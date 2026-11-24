using KeyTapLoop.Core;

namespace KeyTapLoop.Ui;

/// <summary>Everything that isn't needed for a single run: shortcuts, appearance, sound and compatibility.</summary>
internal sealed class SettingsForm : Form
{
    private const int ContentWidth = 350;

    private readonly CaptureButton startButton = new("") { Size = new Size(180, 34), AccessibleName = "Start shortcut. Click, then press the new keys." };
    private readonly CaptureButton stopButton = new("") { Size = new Size(180, 34), AccessibleName = "Stop shortcut. Click, then press the new keys." };
    private readonly CaptureButton toggleButton = new("") { Size = new Size(180, 34), AccessibleName = "Toggle shortcut. Click, then press the new keys." };
    private readonly Label errorLabel = Theme.ErrorLabel(ContentWidth);
    private readonly Button saveButton = new() { Text = "Save", Size = new Size(88, 34), DialogResult = DialogResult.OK };
    private readonly RadioButton autoRadio = new() { Text = "Auto", Size = new Size(84, 32), AccessibleName = "Follow the Windows light or dark setting" };
    private readonly RadioButton lightRadio = new() { Text = "Light", Size = new Size(84, 32), AccessibleName = "Always light" };
    private readonly RadioButton darkRadio = new() { Text = "Dark", Size = new Size(84, 32), AccessibleName = "Always dark" };
    private readonly CheckRow soundCheck = new() { Text = "Play a sound when a run starts and ends", AccessibleName = "Play a short sound when a run starts and ends" };
    private readonly CheckRow scanCodeCheck = new() { Text = "Send hardware scan codes", AccessibleName = "Send hardware scan codes. Recommended; games usually need this." };

    public KeyChord Start { get; private set; }
    public KeyChord Stop { get; private set; }
    public KeyChord Toggle { get; private set; }

    public string ThemeMode => lightRadio.Checked ? "Light" : darkRadio.Checked ? "Dark" : "Auto";
    public bool SoundCues => soundCheck.Checked;
    public bool UseScanCodes => scanCodeCheck.Checked;

    public SettingsForm(KeyChord start, KeyChord stop, KeyChord toggle, string theme, bool soundCues, bool useScanCodes)
    {
        Start = start;
        Stop = stop;
        Toggle = toggle;

        Text = "Settings";
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
        Padding = new Padding(28, 24, 28, 22);
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        var root = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };

        // Shortcuts
        var shortcuts = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Margin = Padding.Empty };
        shortcuts.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
        shortcuts.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 270));
        void AddRow(int row, string name, CaptureButton button)
        {
            var label = Theme.Label(name);
            label.Anchor = AnchorStyles.Left;
            button.Margin = new Padding(0, 0, 0, 8);
            shortcuts.Controls.Add(label, 0, row);
            shortcuts.Controls.Add(button, 1, row);
        }
        AddRow(0, "Start", startButton);
        AddRow(1, "Stop", stopButton);
        AddRow(2, "Toggle", toggleButton);

        var shortcutHint = Theme.Label("Click a box, then press the keys. Escape cancels. Each needs Ctrl, Alt or Shift. Toggle starts a run, or stops the one that is running.", muted: true, size: 9f);
        shortcutHint.MaximumSize = new Size(ContentWidth, 0);
        shortcutHint.Margin = new Padding(0, 0, 0, 12);
        errorLabel.Margin = new Padding(0, 0, 0, 4);

        root.Controls.Add(Theme.Section("Shortcuts"));
        root.Controls.Add(shortcutHint);
        root.Controls.Add(shortcuts);
        root.Controls.Add(errorLabel);

        // Appearance
        var look = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = Padding.Empty };
        foreach (var r in new[] { autoRadio, lightRadio, darkRadio })
        {
            Theme.StyleToggle(r);
            r.Margin = new Padding(0, 0, 4, 0);
            look.Controls.Add(r);
        }
        (theme == "Light" ? lightRadio : theme == "Dark" ? darkRadio : autoRadio).Checked = true;
        var lookCaption = Theme.Section("Appearance");
        lookCaption.Margin = new Padding(0, 14, 0, 8);
        root.Controls.Add(lookCaption);
        root.Controls.Add(look);

        // Sound and compatibility
        var more = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = Padding.Empty };
        soundCheck.Checked = soundCues;
        scanCodeCheck.Checked = useScanCodes;
        soundCheck.Margin = new Padding(0, 0, 0, 12);
        var scanHint = Theme.Label("Recommended. Games usually need it. Turn it off only if an ordinary app misbehaves.", muted: true, size: 9f);
        scanHint.MaximumSize = new Size(ContentWidth, 0);
        scanHint.Margin = new Padding(26, 3, 0, 0);
        more.Controls.Add(soundCheck);
        more.Controls.Add(scanCodeCheck);
        more.Controls.Add(scanHint);
        var moreCaption = Theme.Section("Sound and compatibility");
        moreCaption.Margin = new Padding(0, 18, 0, 8);
        root.Controls.Add(moreCaption);
        root.Controls.Add(more);

        // Buttons
        var reset = new Button { Text = "Reset shortcuts", AutoSize = true, MinimumSize = new Size(0, 34), AccessibleName = "Reset the shortcuts to their defaults" };
        var cancel = new Button { Text = "Cancel", Size = new Size(88, 34), DialogResult = DialogResult.Cancel };
        Theme.StyleButton(reset, quiet: true);
        Theme.StyleButton(cancel);
        Theme.StyleButton(saveButton, primary: true);
        reset.Margin = new Padding(-8, 0, 40, 0);
        cancel.Margin = new Padding(0, 0, 8, 0);
        var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(0, 22, 0, 0) };
        buttons.Controls.AddRange(new Control[] { reset, cancel, saveButton });
        root.Controls.Add(buttons);

        Controls.Add(root);
        AcceptButton = saveButton;
        CancelButton = cancel;

        startButton.Captured += chord => { Start = chord; Refresh_(); };
        stopButton.Captured += chord => { Stop = chord; Refresh_(); };
        toggleButton.Captured += chord => { Toggle = chord; Refresh_(); };
        reset.Click += (_, _) =>
        {
            Start = AppSettings.DefaultStartHotkey;
            Stop = AppSettings.DefaultStopHotkey;
            Toggle = AppSettings.DefaultToggleHotkey;
            Refresh_();
        };
        Refresh_();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Theme.StyleWindow(this);
    }

    private void Refresh_()
    {
        startButton.ShownText = Start.ToString();
        stopButton.ShownText = Stop.ToString();
        toggleButton.ShownText = Toggle.ToString();
        var problem = RunValidator.ValidateHotkeys(Start, Stop, Toggle);
        Theme.SetError(errorLabel, problem);
        saveButton.Enabled = problem is null;
    }
}
