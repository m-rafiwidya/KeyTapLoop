using System.Text;

namespace KeyTapLoop.Native;

/// <summary>
/// The window a run is bound to. Identity is the window handle plus the owning process
/// (id and start time), so a recycled handle or process id is never mistaken for the original.
/// Lives only for the current app session and is never written to disk.
/// </summary>
internal sealed class TargetWindow
{
    private static readonly string[] ShellClasses = { "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd" };

    public required IntPtr Handle { get; init; }
    public required uint ProcessId { get; init; }
    public required string Title { get; init; }
    public required string ProcessName { get; init; }
    public bool IsPracticePad { get; init; }

    /// <summary>True if the process may run with higher rights than this app, so Windows can ignore our input.</summary>
    public bool MayBeElevated { get; init; }

    private long processStartTime;

    public string Display => $"{(Title.Length > 0 ? Title : "(untitled window)")}  ·  {ProcessName}";

    public bool IsAlive()
    {
        if (!NativeMethods.IsWindow(Handle)) return false;
        NativeMethods.GetWindowThreadProcessId(Handle, out var pid);
        if (pid != ProcessId) return false;
        return processStartTime == 0 || GetStartTime(pid) == processStartTime;
    }

    public bool IsForeground() => NativeMethods.GetForegroundWindow() == Handle;

    /// <summary>Describes a window, or returns null with a reason if it can't be a target.</summary>
    public static TargetWindow? TryDescribe(IntPtr handle, bool practicePad, out string error)
    {
        error = "";
        if (handle == IntPtr.Zero || !NativeMethods.IsWindow(handle))
        {
            error = "No window was in the foreground. Click the window you want, then try again.";
            return null;
        }

        var className = new StringBuilder(256);
        NativeMethods.GetClassNameW(handle, className, className.Capacity);
        if (ShellClasses.Contains(className.ToString()))
        {
            error = "That’s the desktop or taskbar. Click an app window instead.";
            return null;
        }

        NativeMethods.GetWindowThreadProcessId(handle, out var pid);
        var title = new StringBuilder(512);
        NativeMethods.GetWindowTextW(handle, title, title.Capacity);

        var target = new TargetWindow
        {
            Handle = handle,
            ProcessId = pid,
            Title = title.ToString(),
            ProcessName = practicePad ? "KeyTapLoop practice pad" : GetProcessName(pid),
            IsPracticePad = practicePad,
            MayBeElevated = !practicePad && MayOutrankUs(pid),
        };
        target.processStartTime = GetStartTime(pid);
        return target;
    }

    private static string GetProcessName(uint pid)
    {
        var handle = NativeMethods.OpenProcess(NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (handle == IntPtr.Zero) return "unknown process";
        try
        {
            var buffer = new StringBuilder(1024);
            var size = (uint)buffer.Capacity;
            if (!NativeMethods.QueryFullProcessImageNameW(handle, 0, buffer, ref size)) return "unknown process";
            return Path.GetFileName(buffer.ToString());
        }
        finally { NativeMethods.CloseHandle(handle); }
    }

    private static long GetStartTime(uint pid)
    {
        var handle = NativeMethods.OpenProcess(NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (handle == IntPtr.Zero) return 0;
        try { return NativeMethods.GetProcessTimes(handle, out var created, out _, out _, out _) ? created : 0; }
        finally { NativeMethods.CloseHandle(handle); }
    }

    private static readonly bool WeAreElevated = Elevation.IsElevated;

    /// <summary>
    /// Windows silently drops simulated input sent to a higher-integrity window and reports no error,
    /// so we warn up front. If the process token can't be read at all, assume it may outrank us.
    /// </summary>
    private static bool MayOutrankUs(uint pid)
    {
        if (WeAreElevated) return false;
        var process = NativeMethods.OpenProcess(NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (process == IntPtr.Zero) return true;
        try
        {
            if (!NativeMethods.OpenProcessToken(process, NativeMethods.TOKEN_QUERY, out var token)) return true;
            try
            {
                return !NativeMethods.GetTokenInformation(token, NativeMethods.TokenElevation, out var elevated, sizeof(int), out _)
                       || elevated != 0;
            }
            finally { NativeMethods.CloseHandle(token); }
        }
        finally { NativeMethods.CloseHandle(process); }
    }
}
