using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;

namespace KeyTapLoop.Native;

/// <summary>Helpers for the case where the target window runs as administrator and Windows would ignore our keys.</summary>
internal static class Elevation
{
    public const string RestartedFlag = "--restarted";
    private const int ErrorCancelled = 1223; // the user said No at the UAC prompt

    public static bool IsElevated
    {
        get
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
    }

    /// <summary>
    /// Starts a second copy of this app with administrator rights. The caller should close this copy
    /// when it returns true. Settings are saved first, and no window choice is carried over.
    /// </summary>
    public static bool TryRestartElevated(out string error)
    {
        error = "";
        var path = Environment.ProcessPath;
        if (string.IsNullOrEmpty(path))
        {
            error = "Couldn\u2019t find KeyTapLoop\u2019s own file to restart it.";
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo(path, RestartedFlag) { UseShellExecute = true, Verb = "runas" });
            return true;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == ErrorCancelled)
        {
            error = "Administrator permission was declined, so KeyTapLoop stayed as it was.";
            return false;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            error = "Windows couldn\u2019t restart KeyTapLoop as administrator: " + ex.Message;
            return false;
        }
    }
}
