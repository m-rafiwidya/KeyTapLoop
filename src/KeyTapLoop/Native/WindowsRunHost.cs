using System.Diagnostics;
using System.Runtime.InteropServices;
using KeyTapLoop.Core;

namespace KeyTapLoop.Native;

/// <summary>Monotonic clock with waits that wake immediately when a run is cancelled.</summary>
internal sealed class SystemRunClock : IRunClock
{
    public long NowMs => Stopwatch.GetTimestamp() * 1000 / Stopwatch.Frequency;

    public async Task DelayUntilAsync(long targetMs, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var remaining = targetMs - NowMs;
        if (remaining <= 0) return;

        // The cancel callback may fire on the UI thread. RunContinuationsAsynchronously keeps the
        // run's cleanup (which calls SendInput) off that thread.
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = token.Register(() => cancelled.TrySetResult());
        await Task.WhenAny(Task.Delay((int)Math.Min(remaining, int.MaxValue)), cancelled.Task).ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
    }
}

/// <summary>Sends real key events with SendInput to whatever window is in the foreground.</summary>
internal sealed class WindowsRunHost : IRunHost
{
    private readonly TargetWindow? target;
    private readonly IntPtr ownWindow;
    private readonly bool useScanCodes;

    /// <param name="target">The chosen window, or null to send to whichever window is in front.</param>
    /// <param name="ownWindow">KeyTapLoop's own window, which is never a valid place to send keys.</param>
    /// <param name="useScanCodes">Send hardware scan codes, which games that read the keyboard directly usually need.</param>
    public WindowsRunHost(TargetWindow? target, IntPtr ownWindow, bool useScanCodes)
    {
        this.target = target;
        this.ownWindow = ownWindow;
        this.useScanCodes = useScanCodes;
    }

    public bool IsTargetAlive() => target?.IsAlive() ?? true;

    public bool IsTargetForeground()
    {
        if (target is not null) return target.IsForeground();
        var front = NativeMethods.GetForegroundWindow();
        return front != IntPtr.Zero && front != ownWindow; // "any window" still never types into this app
    }

    public bool KeyDown(int virtualKey, bool extended) => Send(virtualKey, extended, keyUp: false, useScanCodes);
    public bool KeyUp(int virtualKey, bool extended) => Send(virtualKey, extended, keyUp: true, useScanCodes);

    public bool IsKeyHeld(int virtualKey) => (NativeMethods.GetAsyncKeyState(virtualKey) & 0x8000) != 0;

    /// <summary>
    /// Returns true only if Windows queued the event. That is all it proves: another program can
    /// still ignore simulated input, and a higher-privilege window can have it blocked without any error.
    /// </summary>
    private static bool Send(int virtualKey, bool extended, bool keyUp, bool useScanCodes)
    {
        var flags = (keyUp ? NativeMethods.KEYEVENTF_KEYUP : 0u) | (extended ? NativeMethods.KEYEVENTF_EXTENDEDKEY : 0u);
        var scan = (ushort)NativeMethods.MapVirtualKeyW((uint)virtualKey, NativeMethods.MAPVK_VK_TO_VSC);
        var vk = (ushort)virtualKey;

        // Scan-code events describe a physical key rather than a character, which is what games that
        // read the keyboard directly look at. If Windows has no scan code for the key, fall back to the virtual key.
        if (useScanCodes && scan != 0)
        {
            flags |= NativeMethods.KEYEVENTF_SCANCODE;
            vk = 0;
        }

        var input = new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_KEYBOARD,
            u = new NativeMethods.INPUTUNION
            {
                ki = new NativeMethods.KEYBDINPUT
                {
                    wVk = vk,
                    wScan = scan,
                    dwFlags = flags,
                    time = 0,
                    dwExtraInfo = UIntPtr.Zero,
                },
            },
        };
        return NativeMethods.SendInput(1, new[] { input }, NativeMethods.InputSize) == 1;
    }

    /// <summary>Guards against a wrong struct layout, which would make every SendInput call fail.</summary>
    public static bool StructLayoutIsValid => NativeMethods.InputSize == (Environment.Is64BitProcess ? 40 : 28);
}

/// <summary>Raises <see cref="Changed"/> whenever Windows moves the foreground to a different top-level window.</summary>
internal sealed class ForegroundWatcher : IDisposable
{
    // The delegate is held in a field so the garbage collector can't free it while Windows still calls it.
    private readonly NativeMethods.WinEventProc callback;
    private IntPtr hook;

    public event Action<IntPtr>? Changed;

    public ForegroundWatcher()
    {
        callback = OnWinEvent;
        hook = NativeMethods.SetWinEventHook(
            NativeMethods.EVENT_SYSTEM_FOREGROUND, NativeMethods.EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero, callback, 0, 0, NativeMethods.WINEVENT_OUTOFCONTEXT);
        if (hook == IntPtr.Zero)
            throw new InvalidOperationException("Windows refused the focus-change subscription (error " + Marshal.GetLastWin32Error() + ").");
    }

    private void OnWinEvent(IntPtr hookHandle, uint eventType, IntPtr hwnd, int idObject, int idChild, uint threadId, uint timeMs)
    {
        if (eventType == NativeMethods.EVENT_SYSTEM_FOREGROUND && hwnd != IntPtr.Zero)
            Changed?.Invoke(hwnd);
    }

    public void Dispose()
    {
        if (hook == IntPtr.Zero) return;
        NativeMethods.UnhookWinEvent(hook);
        hook = IntPtr.Zero;
        GC.KeepAlive(callback);
    }
}

/// <summary>Registers a global shortcut on a window and reports why it failed if it can't.</summary>
internal static class Hotkeys
{
    private const int ErrorHotkeyAlreadyRegistered = 1409;

    public static bool TryRegister(IntPtr window, int id, KeyChord chord, out string error)
    {
        var modifiers = NativeMethods.MOD_NOREPEAT;
        if (chord.Ctrl) modifiers |= NativeMethods.MOD_CONTROL;
        if (chord.Alt) modifiers |= NativeMethods.MOD_ALT;
        if (chord.Shift) modifiers |= NativeMethods.MOD_SHIFT;

        if (NativeMethods.RegisterHotKey(window, id, modifiers, (uint)chord.VirtualKey))
        {
            error = "";
            return true;
        }

        var code = Marshal.GetLastWin32Error();
        error = code == ErrorHotkeyAlreadyRegistered
            ? $"{chord} is already used by another app."
            : $"Windows wouldn’t register {chord} (error {code}).";
        return false;
    }

    public static void Unregister(IntPtr window, int id) => NativeMethods.UnregisterHotKey(window, id);
}
