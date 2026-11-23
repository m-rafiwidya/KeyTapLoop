using KeyTapLoop.Core;
using KeyTapLoop.Native;
using KeyTapLoop.Ui;

namespace KeyTapLoop;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        Theme.Choose(SettingsStore.Load(SettingsStore.DefaultPath).Settings.Theme);

        // One copy at a time: two copies would fight over the same global shortcuts.
        // A copy started by "Restart as administrator" waits briefly for the old one to close.
        var restarted = args.Contains(Elevation.RestartedFlag);
        using var singleInstance = new Mutex(initiallyOwned: false, @"Local\KeyTapLoop.SingleInstance");
        var owned = false;
        try { owned = singleInstance.WaitOne(restarted ? TimeSpan.FromSeconds(8) : TimeSpan.Zero); }
        catch (AbandonedMutexException) { owned = true; } // the previous copy was killed; the lock is ours now

        if (!owned)
        {
            MessageBox.Show("KeyTapLoop is already running.", "KeyTapLoop", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            if (!WindowsRunHost.StructLayoutIsValid)
            {
                MessageBox.Show(
                    "This build can\u2019t send keys on this system (unexpected input structure size). Use the 64-bit Windows build.",
                    "KeyTapLoop", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            Application.Run(new MainForm());
        }
        finally
        {
            singleInstance.ReleaseMutex();
        }
    }
}
