using KeyTapLoop.Native;
using KeyTapLoop.Ui;

namespace KeyTapLoop;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        // One copy at a time: two copies would fight over the same global shortcuts.
        using var singleInstance = new Mutex(initiallyOwned: true, @"Local\KeyTapLoop.SingleInstance", out var isFirst);
        if (!isFirst)
        {
            MessageBox.Show("KeyTapLoop is already running.", "KeyTapLoop", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (!WindowsRunHost.StructLayoutIsValid)
        {
            MessageBox.Show(
                "This build can’t send keys on this system (unexpected input structure size). Use the 64-bit Windows build.",
                "KeyTapLoop", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        Application.Run(new MainForm());
    }
}
