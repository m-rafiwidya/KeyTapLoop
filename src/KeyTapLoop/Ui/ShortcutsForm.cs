using KeyTapLoop.Core;

namespace KeyTapLoop.Ui;

/// <summary>Lets the user change the global Start and Stop shortcuts.</summary>
internal sealed class ShortcutsForm : Form
{
    private readonly CaptureButton startButton = new("") { Size = new Size(190, 34), AccessibleName = "Start shortcut. Click, then press the new keys." };
    private readonly CaptureButton stopButton = new("") { Size = new Size(190, 34), AccessibleName = "Stop shortcut. Click, then press the new keys." };
    private readonly Label errorLabel = Theme.ErrorLabel(330);
    private readonly Button saveButton = new() { Text = "Save", Size = new Size(88, 34), DialogResult = DialogResult.OK };

    public KeyChord Start { get; private set; }
    public KeyChord Stop { get; private set; }

    public ShortcutsForm(KeyChord start, KeyChord stop)
    {
        Start = start;
        Stop = stop;

        Text = "Shortcuts";
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96f, 96f);
        BackColor = Theme.Background;
        ForeColor = Theme.Text;
        Font = Theme.Body();
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Padding = new Padding(24, 22, 24, 20);
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        var grid = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, RowCount = 4 };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));

        var heading = Theme.Label("Global shortcuts", size: 13f, style: FontStyle.Bold);
        heading.Margin = new Padding(0, 0, 0, 4);
        var hint = Theme.Label("Click a box, then press the keys. Escape cancels. Each shortcut needs Ctrl, Alt or Shift.", muted: true, size: 9f);
        hint.MaximumSize = new Size(340, 0);
        hint.Margin = new Padding(0, 0, 0, 14);

        grid.Controls.Add(heading, 0, 0); grid.SetColumnSpan(heading, 2);
        grid.Controls.Add(hint, 0, 1); grid.SetColumnSpan(hint, 2);

        var startLabel = Theme.Label("Start"); startLabel.Anchor = AnchorStyles.Left;
        var stopLabel = Theme.Label("Stop"); stopLabel.Anchor = AnchorStyles.Left;
        startButton.Margin = new Padding(0, 0, 0, 8);
        stopButton.Margin = new Padding(0, 0, 0, 8);
        grid.Controls.Add(startLabel, 0, 2);
        grid.Controls.Add(startButton, 1, 2);
        grid.Controls.Add(stopLabel, 0, 3);
        grid.Controls.Add(stopButton, 1, 3);

        var reset = new Button { Text = "Reset", Size = new Size(80, 34), AccessibleName = "Reset shortcuts to defaults" };
        var cancel = new Button { Text = "Cancel", Size = new Size(88, 34), DialogResult = DialogResult.Cancel };
        Theme.StyleButton(reset, quiet: true);
        Theme.StyleButton(cancel);
        Theme.StyleButton(saveButton, primary: true);
        saveButton.Margin = new Padding(8, 0, 0, 0);
        cancel.Margin = new Padding(8, 0, 0, 0);

        var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(0, 10, 0, 0) };
        buttons.Controls.AddRange(new Control[] { reset, cancel, saveButton });

        errorLabel.Margin = new Padding(0, 0, 0, 0);

        var root = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        root.Controls.Add(grid);
        root.Controls.Add(errorLabel);
        root.Controls.Add(buttons);
        Controls.Add(root);

        AcceptButton = saveButton;
        CancelButton = cancel;

        startButton.Captured += chord => { Start = chord; Refresh_(); };
        stopButton.Captured += chord => { Stop = chord; Refresh_(); };
        reset.Click += (_, _) =>
        {
            Start = AppSettings.DefaultStartHotkey;
            Stop = AppSettings.DefaultStopHotkey;
            Refresh_();
        };
        Refresh_();
    }

    private void Refresh_()
    {
        startButton.ShownText = Start.ToString();
        stopButton.ShownText = Stop.ToString();
        var problem = RunValidator.ValidateHotkeys(Start, Stop);
        Theme.SetError(errorLabel, problem);
        saveButton.Enabled = problem is null;
    }
}
