using System.Globalization;
using KeyTapLoop.Core;
using KeyTapLoop.Native;

namespace KeyTapLoop.Ui;

internal sealed class MainForm : Form
{
    private const int StartHotkeyId = 1;
    private const int StopHotkeyId = 2;
    private const int LeftWidth = 360;
    private const int RightWidth = 300;
    private const int ColumnGap = 36;

    private readonly string settingsPath = SettingsStore.DefaultPath;
    private readonly SystemRunClock clock = new();
    private readonly ForegroundWatcher watcher = new();
    private readonly System.Windows.Forms.Timer uiTimer = new() { Interval = 100 };
    private readonly System.Windows.Forms.Timer pickTimer = new() { Interval = 1000 };

    // Output
    private readonly CheckBox ctrlToggle = new() { Text = "Ctrl", Size = new Size(52, 32), AccessibleName = "Ctrl modifier" };
    private readonly CheckBox altToggle = new() { Text = "Alt", Size = new Size(52, 32), AccessibleName = "Alt modifier" };
    private readonly CheckBox shiftToggle = new() { Text = "Shift", Size = new Size(56, 32), AccessibleName = "Shift modifier" };
    private readonly ComboBox keyBox = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 98, FlatStyle = FlatStyle.Flat, AccessibleName = "Key" };
    private readonly CaptureButton captureButton = new("Capture") { Size = new Size(72, 32), AccessibleName = "Capture a key press" };
    private readonly Label outputPreview = Theme.Label("", muted: true, size: 9f);
    private readonly Label outputError = Theme.ErrorLabel(LeftWidth);
    private readonly CheckBox scanCodeCheck = new() { Text = "Send as hardware scan codes (try this for games)", AutoSize = true, Checked = true, AccessibleName = "Send hardware scan codes. Games that read the keyboard directly usually need this." };

    // Mode and timing
    private readonly RadioButton repeatRadio = new() { Text = "Repeat", Checked = true, Size = new Size(96, 32), AccessibleName = "Repeat mode: tap the key again and again" };
    private readonly RadioButton holdRadio = new() { Text = "Hold", Size = new Size(96, 32), AccessibleName = "Hold mode: keep the key down" };
    private readonly TextField periodField = new(88, "Period in milliseconds, start to start");
    private readonly TextField keyDownField = new(88, "Key-down time in milliseconds");
    private readonly CheckBox countCheck = new() { Text = "Stop after", AutoSize = true, Checked = true, AccessibleName = "Limit by tap count" };
    private readonly TextField countField = new(88, "Maximum number of taps");
    private readonly CheckBox durationCheck = new() { Text = "Stop after", AutoSize = true, AccessibleName = "Limit by time" };
    private readonly TextField durationField = new(88, "Time limit in seconds");
    private readonly TextField delayField = new(88, "Start delay in seconds");
    private readonly Label limitHint = Theme.Label("", muted: true, size: 9f);
    private readonly Dictionary<Field, Label> fieldErrors = new();
    private readonly Dictionary<Field, TextField> fieldBoxes = new();

    // Target
    private readonly RadioButton chosenRadio = new() { Text = "Chosen window", Checked = true, Size = new Size(132, 32), AccessibleName = "Send keys to one chosen window only" };
    private readonly RadioButton anyRadio = new() { Text = "Any window", Size = new Size(112, 32), AccessibleName = "Send keys to whichever window is in front" };
    private readonly FlowLayoutPanel chosenPanel = new() { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = Padding.Empty };
    private readonly Label anyWindowNote = Theme.Label("", size: 8.75f);
    private readonly Button adminButton = new() { Text = "Restart as administrator", Size = new Size(184, 32), AccessibleName = "Restart KeyTapLoop as administrator so it can send keys to an administrator window" };
    private readonly Button pickButton = new() { Text = "Choose window", Size = new Size(124, 34), AccessibleName = "Choose the target window. Counts down three seconds, then uses the window in front." };
    private readonly Button padButton = new() { Text = "Use practice pad", AutoSize = true, MinimumSize = new Size(0, 30), AccessibleName = "Use the built-in practice pad as the target" };
    private readonly Label targetLabel = Theme.Label("No window chosen", muted: true);
    private readonly Label targetWarning = Theme.Label("", size: 8.75f);

    // Run controls and result
    private readonly Button startButton = new() { Size = new Size(144, 42), AccessibleName = "Start" };
    private readonly Button stopButton = new() { Size = new Size(144, 42), AccessibleName = "Stop", Enabled = false };
    private readonly Label noticeLabel = Theme.Label("", size: 9f);
    private readonly Label stateLabel = Theme.Label("Ready", size: 14f, style: FontStyle.Bold);
    private readonly Label detailLabel = Theme.Label("", muted: true, size: 9f);
    private readonly Label statsLabel = Theme.Label("", size: 9.5f);
    private readonly Button shortcutsButton = new() { Text = "Shortcuts…", AutoSize = true, MinimumSize = new Size(0, 28), AccessibleName = "Change the Start and Stop shortcuts" };

    // State
    private AppSettings lastValid;
    private KeyChord startHotkey;
    private KeyChord stopHotkey;
    private TargetWindow? target;
    private PracticePadForm? pad;
    private RunSession? session;
    private Task<RunResult>? runTask;
    private bool loading = true;
    private bool busy;
    private bool picking;
    private int pickLeft;
    private bool lastRunWasHold;
    private bool runIsAnyWindow;
    private RunOptions? activeOptions;
    private readonly List<string> startupNotes = new();

    public MainForm()
    {
        var loaded = SettingsStore.Load(settingsPath);
        lastValid = loaded.Settings;
        startHotkey = lastValid.StartChord;
        stopHotkey = lastValid.StopChord;
        if (loaded.Warning is not null) startupNotes.Add(loaded.Warning);

        Text = Elevation.IsElevated ? "KeyTapLoop (administrator)" : "KeyTapLoop";
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96f, 96f);
        BackColor = Theme.Background;
        ForeColor = Theme.Text;
        Font = Theme.Body();
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Padding = new Padding(30, 26, 30, 20);
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { /* the default icon is fine */ }

        StyleInputs();
        Controls.Add(BuildLayout());
        WireEvents();

        keyBox.Items.AddRange(KeyCatalog.All.Cast<object>().ToArray());
        ApplySettings(lastValid);
        loading = false;
        UpdateModeState();
        Revalidate();
        UpdateButtonLabels();
        UpdateTargetLabel();
        ShowIdle();
    }

    // ===================================================================================
    //  Layout
    // ===================================================================================

    private void StyleInputs()
    {
        foreach (var t in new[] { ctrlToggle, altToggle, shiftToggle }) Theme.StyleToggle(t);
        foreach (var r in new[] { repeatRadio, holdRadio, chosenRadio, anyRadio }) Theme.StyleToggle(r);
        Theme.StyleButton(pickButton);
        Theme.StyleButton(adminButton);
        Theme.StyleButton(padButton, quiet: true);
        Theme.StyleButton(startButton, primary: true);
        Theme.StyleButton(stopButton);
        Theme.StyleButton(shortcutsButton, quiet: true);

        keyBox.BackColor = Theme.Surface;
        keyBox.ForeColor = Theme.Text;
        keyBox.Font = Theme.Body();
        keyBox.MaxDropDownItems = 12;

        foreach (var c in new[] { countCheck, durationCheck, scanCodeCheck })
        {
            c.ForeColor = Theme.Text;
            c.BackColor = Color.Transparent;
            c.Font = Theme.Body();
            c.Cursor = Cursors.Hand;
        }

        targetWarning.ForeColor = Theme.Warn;
        targetWarning.MaximumSize = new Size(RightWidth, 0);
        targetWarning.Margin = new Padding(0, 8, 0, 0);
        targetWarning.Visible = false;

        adminButton.Margin = new Padding(0, 10, 0, 0);
        adminButton.Visible = false;

        anyWindowNote.ForeColor = Theme.Warn;
        anyWindowNote.MaximumSize = new Size(RightWidth, 0);
        anyWindowNote.Margin = new Padding(0, 12, 0, 0);
        anyWindowNote.Visible = false;

        limitHint.MaximumSize = new Size(LeftWidth, 0);
        limitHint.Margin = new Padding(0, 2, 0, 0);

        targetLabel.MaximumSize = new Size(RightWidth - 134, 0);
        targetLabel.Margin = new Padding(12, 0, 0, 0);
        targetLabel.Anchor = AnchorStyles.Left;

        noticeLabel.ForeColor = Theme.Warn;
        noticeLabel.MaximumSize = new Size(RightWidth, 0);
        noticeLabel.Margin = new Padding(0, 10, 0, 0);
        noticeLabel.Visible = false;
    }

    private Control BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 3,
            RowCount = 3,
            BackColor = Color.Transparent,
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, LeftWidth));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, ColumnGap));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, RightWidth));

        // Header
        var header = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = new Padding(0, 0, 0, 6) };
        var title = Theme.Label("KeyTapLoop", size: 18f, style: FontStyle.Bold);
        var tagline = Theme.Label("Repeat or hold a key. By default it stops the moment focus leaves your chosen window.", muted: true, size: 9.5f);
        tagline.Margin = new Padding(0, 8, 0, 0);
        header.Controls.Add(title);
        header.Controls.Add(tagline);
        root.Controls.Add(header, 0, 0);
        root.SetColumnSpan(header, 3);

        // Left column: what to send and how
        var left = Column();
        left.Controls.Add(Block("Key", BuildKeyRow(), first: true));
        left.Controls.Add(Block("Mode", BuildModeRow()));
        left.Controls.Add(Block("Timing", BuildTimingGrid()));
        root.Controls.Add(left, 0, 1);

        // Right column: where, go, and what happened
        var right = Column();
        right.Controls.Add(Block("Target", BuildTargetBlock(), first: true));
        right.Controls.Add(BuildRunButtons());
        right.Controls.Add(noticeLabel);
        right.Controls.Add(BuildResultCard());
        root.Controls.Add(right, 2, 1);

        // Footer
        var footer = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(0, 18, 0, 0) };
        shortcutsButton.Margin = new Padding(-8, 0, 0, 0);
        var version = Theme.Label($"v{typeof(MainForm).Assembly.GetName().Version?.ToString(3) ?? "0.1.0"}  ·  free and open source  ·  no network, no telemetry{(Elevation.IsElevated ? "  ·  administrator" : "")}", muted: true, size: 8.5f);
        version.Margin = new Padding(14, 7, 0, 0);
        footer.Controls.Add(shortcutsButton);
        footer.Controls.Add(version);
        root.Controls.Add(footer, 0, 2);
        root.SetColumnSpan(footer, 3);

        return root;
    }

    private static TableLayoutPanel Column()
    {
        var column = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Dock = DockStyle.Fill, Margin = Padding.Empty };
        column.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        return column;
    }

    private static Control Block(string caption, Control body, bool first = false)
    {
        var block = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = new Padding(0, first ? 0 : 22, 0, 0),
        };
        block.Controls.Add(Theme.Section(caption));
        block.Controls.Add(body);
        return block;
    }

    private Control BuildKeyRow()
    {
        var wrap = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };

        var row = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        ctrlToggle.Margin = new Padding(0, 0, 4, 0);
        altToggle.Margin = new Padding(0, 0, 4, 0);
        shiftToggle.Margin = new Padding(0, 0, 10, 0);
        keyBox.Margin = new Padding(0, 4, 6, 0);
        row.Controls.AddRange(new Control[] { ctrlToggle, altToggle, shiftToggle, keyBox, captureButton });

        outputPreview.Margin = new Padding(0, 8, 0, 0);
        wrap.Controls.Add(row);
        scanCodeCheck.Margin = new Padding(0, 10, 0, 0);
        wrap.Controls.Add(outputPreview);
        wrap.Controls.Add(outputError);
        wrap.Controls.Add(scanCodeCheck);
        return wrap;
    }

    private Control BuildModeRow()
    {
        var wrap = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        repeatRadio.Margin = new Padding(0, 0, 4, 0);
        wrap.Controls.Add(repeatRadio);
        wrap.Controls.Add(holdRadio);
        return wrap;
    }

    private Control BuildTimingGrid()
    {
        var grid = new TableLayoutPanel { AutoSize = true, ColumnCount = 3 };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 116));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, LeftWidth - 212));

        var row = 0;
        void Add(Control label, TextField field, string unit, Field key)
        {
            label.Anchor = AnchorStyles.Left;
            field.Margin = new Padding(0, 0, 8, 0);
            var unitLabel = Theme.Label(unit, muted: true, size: 9f);
            unitLabel.Anchor = AnchorStyles.Left;
            var error = Theme.ErrorLabel(LeftWidth);
            error.Margin = new Padding(0, 2, 0, 0);

            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            grid.Controls.Add(label, 0, row);
            grid.Controls.Add(field, 1, row);
            grid.Controls.Add(unitLabel, 2, row);
            grid.Controls.Add(error, 0, row + 1);
            grid.SetColumnSpan(error, 3);
            fieldErrors[key] = error;
            fieldBoxes[key] = field;
            field.Margin = new Padding(0, row == 0 ? 0 : 0, 8, 6);
            row += 2;
        }

        Add(Theme.Label("Every"), periodField, "ms, start to start", Field.Period);
        Add(Theme.Label("Key down for"), keyDownField, "ms per tap", Field.KeyDown);
        Add(countCheck, countField, "taps", Field.Count);
        Add(durationCheck, durationField, "seconds", Field.Duration);
        Add(Theme.Label("Start delay"), delayField, "seconds, Start button only", Field.Delay);

        var wrap = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        wrap.Controls.Add(grid);
        wrap.Controls.Add(limitHint);
        return wrap;
    }

    private Control BuildTargetBlock()
    {
        var wrap = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };

        var mode = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(0, 0, 0, 12) };
        chosenRadio.Margin = new Padding(0, 0, 4, 0);
        mode.Controls.Add(chosenRadio);
        mode.Controls.Add(anyRadio);

        var row = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        row.Controls.Add(pickButton);
        row.Controls.Add(targetLabel);

        padButton.Margin = new Padding(-8, 6, 0, 0);
        chosenPanel.Controls.Add(row);
        chosenPanel.Controls.Add(padButton);
        chosenPanel.Controls.Add(targetWarning);
        chosenPanel.Controls.Add(adminButton);

        wrap.Controls.Add(mode);
        wrap.Controls.Add(chosenPanel);
        wrap.Controls.Add(anyWindowNote);
        return wrap;
    }

    private Control BuildRunButtons()
    {
        var row = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(0, 22, 0, 0) };
        startButton.Margin = new Padding(0, 0, 12, 0);
        row.Controls.Add(startButton);
        row.Controls.Add(stopButton);
        return row;
    }

    private Control BuildResultCard()
    {
        var card = new FlowLayoutPanel
        {
            BackColor = Theme.Surface,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(16, 14, 16, 14),
            Margin = new Padding(0, 18, 0, 0),
            MinimumSize = new Size(RightWidth, 150),
            MaximumSize = new Size(RightWidth, 0),
            AccessibleName = "Result of the last run",
        };
        card.Paint += (_, e) =>
        {
            using var pen = new Pen(Theme.Line);
            e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
        };

        detailLabel.MaximumSize = new Size(RightWidth - 34, 0);
        detailLabel.Margin = new Padding(0, 4, 0, 0);
        statsLabel.MaximumSize = new Size(RightWidth - 34, 0);
        statsLabel.Margin = new Padding(0, 10, 0, 0);
        card.Controls.Add(stateLabel);
        card.Controls.Add(detailLabel);
        card.Controls.Add(statsLabel);
        return card;
    }

    // ===================================================================================
    //  Events and input state
    // ===================================================================================

    private void WireEvents()
    {
        foreach (var t in new[] { ctrlToggle, altToggle, shiftToggle }) t.CheckedChanged += (_, _) => OnInputChanged();
        keyBox.SelectedIndexChanged += (_, _) => OnInputChanged();
        scanCodeCheck.CheckedChanged += (_, _) => OnInputChanged();
        chosenRadio.CheckedChanged += (_, _) => UpdateTargetMode();
        anyRadio.CheckedChanged += (_, _) => UpdateTargetMode();
        adminButton.Click += (_, _) => RestartAsAdministrator();
        repeatRadio.CheckedChanged += (_, _) => OnModeChanged();
        holdRadio.CheckedChanged += (_, _) => OnModeChanged();
        countCheck.CheckedChanged += (_, _) => OnModeChanged();
        durationCheck.CheckedChanged += (_, _) => OnModeChanged();
        foreach (var f in new[] { periodField, keyDownField, countField, durationField, delayField })
            f.ValueChanged += (_, _) => OnInputChanged();

        captureButton.Captured += chord => { SetOutput(chord); };
        pickButton.Click += (_, _) => BeginPickTarget();
        padButton.Click += (_, _) => UsePracticePad();
        startButton.Click += (_, _) => StartRun(fromHotkey: false);
        stopButton.Click += (_, _) => StopRun();
        shortcutsButton.Click += (_, _) => EditShortcuts();

        uiTimer.Tick += (_, _) => ShowProgress();
        pickTimer.Tick += (_, _) => OnPickTick();
        watcher.Changed += hwnd =>
        {
            if (session is null) return;
            if (runIsAnyWindow) session.OnForegroundChanged(hwnd != Handle);   // only KeyTapLoop itself coming forward ends it
            else if (target is not null) session.OnForegroundChanged(hwnd == target.Handle);
        };
    }

    private void OnModeChanged()
    {
        if (loading) return;
        UpdateModeState();
        OnInputChanged();
    }

    private void OnInputChanged()
    {
        if (loading) return;
        Revalidate();
    }

    private bool updatingState;

    /// <summary>Enables the fields that apply to the current mode, and locks everything while a run is active.</summary>
    private void UpdateModeState()
    {
        if (updatingState) return;
        updatingState = true;
        try
        {
            var hold = holdRadio.Checked;

            periodField.Enabled = !hold && !busy;
            keyDownField.Enabled = !hold && !busy;
            countCheck.Enabled = !hold && !busy;
            countField.Enabled = !hold && countCheck.Checked && !busy;
            durationCheck.Enabled = !busy;
            durationField.Enabled = durationCheck.Checked && !busy;
            delayField.Enabled = !busy;

            foreach (var c in new Control[] { ctrlToggle, altToggle, shiftToggle, keyBox, captureButton, repeatRadio, holdRadio, scanCodeCheck, chosenRadio, anyRadio })
                c.Enabled = !busy;

            UpdateLimitHint();
        }
        finally { updatingState = false; }
    }

    /// <summary>Says plainly what happens when no limit is set, instead of treating it as a mistake.</summary>
    private void UpdateLimitHint()
    {
        var hold = holdRadio.Checked;
        var noLimit = hold ? !durationCheck.Checked : !countCheck.Checked && !durationCheck.Checked;
        var end = anyRadio.Checked ? "you press Stop" : "you press Stop or focus leaves the window";
        limitHint.Text = !noLimit ? "" : hold
            ? $"No time limit. The key stays down until {end}."
            : $"No limit. It keeps going until {end}.";
        limitHint.Visible = noLimit;
    }

    private void UpdateTargetMode()
    {
        if (loading || updatingState) return;
        var any = anyRadio.Checked;
        chosenPanel.Visible = !any;
        anyWindowNote.Visible = any;
        anyWindowNote.Text = "Keys go to whichever window is in front, and switching windows won\u2019t end the run. Use Stop, or a limit, to end it.";
        SetNotice(null);
        UpdateLimitHint();
    }

    private RunInput ReadInput()
    {
        KeyChord? output = keyBox.SelectedItem is KeyInfo key
            ? new KeyChord(key.VirtualKey, ctrlToggle.Checked, altToggle.Checked, shiftToggle.Checked)
            : null;

        return new RunInput
        {
            Output = output,
            Mode = holdRadio.Checked ? RunMode.Hold : RunMode.Repeat,
            Period = periodField.Text,
            KeyDown = keyDownField.Text,
            CountEnabled = countCheck.Checked,
            Count = countField.Text,
            DurationEnabled = durationCheck.Checked,
            DurationSeconds = durationField.Text,
            DelaySeconds = delayField.Text,
            StartHotkey = startHotkey,
            StopHotkey = stopHotkey,
        };
    }

    private void SetOutput(KeyChord chord)
    {
        loading = true;
        ctrlToggle.Checked = chord.Ctrl;
        altToggle.Checked = chord.Alt;
        shiftToggle.Checked = chord.Shift;
        keyBox.SelectedItem = KeyCatalog.All.FirstOrDefault(k => k.VirtualKey == chord.VirtualKey);
        loading = false;
        Revalidate();
    }

    private ValidationResult Revalidate()
    {
        var input = ReadInput();
        var result = RunValidator.Validate(input);

        Theme.SetError(outputError, result.Errors.GetValueOrDefault(Field.Output));
        foreach (var (field, label) in fieldErrors)
        {
            Theme.SetError(label, result.Errors.GetValueOrDefault(field));
            fieldBoxes[field].HasError = result.Errors.ContainsKey(field);
        }

        if (input.Output is { } output)
            outputPreview.Text = (input.Mode == RunMode.Hold ? "Holds   " : "Sends   ") + output;
        else
            outputPreview.Text = "No key chosen";

        if (result.Options is { } options) Remember(input, options);
        return result;
    }

    /// <summary>Keeps the last fully valid values so a half-typed field never overwrites a good setting.</summary>
    private void Remember(RunInput input, RunOptions options)
    {
        var s = lastValid.Clone();
        s.Output = options.Output.ToStorageString();
        s.Mode = options.Mode.ToString();
        s.StartDelaySeconds = options.StartDelayMs / 1000;
        s.UseScanCodes = scanCodeCheck.Checked;
        s.StartHotkey = startHotkey.ToStorageString();
        s.StopHotkey = stopHotkey.ToStorageString();
        if (options.Mode == RunMode.Repeat)
        {
            s.PeriodMs = options.PeriodMs;
            s.KeyDownMs = options.KeyDownMs;
            s.CountEnabled = input.CountEnabled;
            s.DurationEnabled = input.DurationEnabled;
            if (options.MaxTaps is { } taps) s.MaxTaps = taps;
        }
        if (options.DurationMs is { } ms) s.DurationMs = ms;
        lastValid = s;
    }

    private void ApplySettings(AppSettings s)
    {
        var wasLoading = loading;
        loading = true;
        var chord = s.OutputChord;
        ctrlToggle.Checked = chord.Ctrl;
        altToggle.Checked = chord.Alt;
        shiftToggle.Checked = chord.Shift;
        keyBox.SelectedItem = KeyCatalog.All.FirstOrDefault(k => k.VirtualKey == chord.VirtualKey);
        periodField.Text = s.PeriodMs.ToString(CultureInfo.InvariantCulture);
        keyDownField.Text = s.KeyDownMs.ToString(CultureInfo.InvariantCulture);
        countCheck.Checked = s.CountEnabled;
        countField.Text = s.MaxTaps.ToString(CultureInfo.InvariantCulture);
        durationCheck.Checked = s.DurationEnabled;
        durationField.Text = (s.DurationMs / 1000.0).ToString("0.###", CultureInfo.InvariantCulture);
        delayField.Text = s.StartDelaySeconds.ToString(CultureInfo.InvariantCulture);
        scanCodeCheck.Checked = s.UseScanCodes;
        (s.RunMode == RunMode.Hold ? holdRadio : repeatRadio).Checked = true;
        loading = wasLoading;
    }

    private void UpdateButtonLabels()
    {
        startButton.Text = $"Start   {startHotkey}";
        stopButton.Text = $"Stop   {stopHotkey}";
        startButton.AccessibleName = $"Start. Shortcut {startHotkey}.";
        stopButton.AccessibleName = $"Stop. Shortcut {stopHotkey}.";
    }

    // ===================================================================================
    //  Choosing a target
    // ===================================================================================

    private void BeginPickTarget()
    {
        if (session is not null || picking) return;
        picking = true;
        pickLeft = 3;
        pickButton.Enabled = false;
        pickButton.Text = $"Picking in {pickLeft}…";
        SetNotice("Switch to the window you want. KeyTapLoop takes whichever window is in front when the countdown ends. Nothing is sent.", warn: false);
        pickTimer.Start();
    }

    private void OnPickTick()
    {
        pickLeft--;
        if (pickLeft > 0)
        {
            pickButton.Text = $"Picking in {pickLeft}…";
            return;
        }
        EndPicking();
        CompletePick(NativeMethods.GetForegroundWindow());
    }

    private void EndPicking()
    {
        pickTimer.Stop();
        picking = false;
        pickButton.Text = "Choose window";
        pickButton.Enabled = !busy;
    }

    private void CompletePick(IntPtr hwnd)
    {
        if (hwnd == Handle)
        {
            SetNotice("That’s KeyTapLoop itself. Switch to the window you want before the countdown ends.");
            return;
        }
        if (pad is { IsDisposed: false } && hwnd == pad.Handle)
        {
            UsePracticePad();
            return;
        }

        var chosen = TargetWindow.TryDescribe(hwnd, practicePad: false, out var error);
        if (chosen is null)
        {
            SetNotice(error);
            return;
        }
        if (chosen.ProcessId == (uint)Environment.ProcessId)
        {
            SetNotice("KeyTapLoop’s own windows can’t be targets, except the practice pad.");
            return;
        }

        target = chosen;
        UpdateTargetLabel();
        SetNotice(null);
    }

    private void UsePracticePad()
    {
        if (session is not null || picking) return;
        if (pad is null || pad.IsDisposed)
        {
            pad = new PracticePadForm();
            pad.FormClosed += (_, _) =>
            {
                if (target is { IsPracticePad: true }) { target = null; UpdateTargetLabel(); }
            };
            PlacePad(pad);
        }
        if (!pad.Visible) pad.Show();
        if (pad.WindowState == FormWindowState.Minimized) pad.WindowState = FormWindowState.Normal;
        pad.Activate();
        pad.FocusTypingArea();

        target = TargetWindow.TryDescribe(pad.Handle, practicePad: true, out _);
        UpdateTargetLabel();
        SetNotice(null);
    }

    private void PlacePad(Form window)
    {
        var area = Screen.FromControl(this).WorkingArea;
        window.StartPosition = FormStartPosition.Manual;
        var x = Right + 16;
        if (x + window.Width > area.Right) x = Math.Max(area.Left, Left - window.Width - 16);
        window.Location = new Point(x, Math.Max(area.Top, Top));
    }

    private void UpdateTargetLabel()
    {
        if (target is null)
        {
            targetLabel.Text = "No window chosen";
            targetLabel.ForeColor = Theme.Muted;
            targetWarning.Visible = false;
            adminButton.Visible = false;
            return;
        }
        targetLabel.Text = target.Display;
        targetLabel.ForeColor = Theme.Text;
        targetWarning.Text = "This app probably runs as administrator. Windows silently blocks keys from a normal KeyTapLoop, even though it reports them as sent.";
        targetWarning.Visible = target.MayBeElevated;
        adminButton.Visible = target.MayBeElevated && !Elevation.IsElevated;
    }

    // ===================================================================================
    //  Running
    // ===================================================================================

    private void StartRun(bool fromHotkey)
    {
        if (session is not null) { SetNotice("A run is already active. Stop it first."); return; }
        if (picking) { SetNotice("Finish choosing a window first."); return; }

        var validation = Revalidate();
        if (!validation.IsValid) { SetNotice("Fix the highlighted fields, then start again."); return; }
        var options = validation.Options!;
        var any = anyRadio.Checked;

        if (!any)
        {
            if (target is null) { SetNotice("Choose a target window first, or switch to Any window."); return; }
            if (!target.IsAlive())
            {
                target = null;
                UpdateTargetLabel();
                SetNotice("That window has closed. Choose a target again.");
                return;
            }

            if (target.IsPracticePad && pad is { IsDisposed: false })
            {
                if (pad.WindowState == FormWindowState.Minimized) pad.WindowState = FormWindowState.Normal;
                if (!fromHotkey) { pad.Activate(); pad.FocusTypingArea(); }
            }
        }

        // The shortcut starts at once, because you are already in the right window. The delay is for the
        // Start button, which leaves KeyTapLoop in front until you switch away.
        if (fromHotkey) options = options with { StartDelayMs = 0 };

        var runTarget = any ? null : target;
        var host = new WindowsRunHost(runTarget, Handle, lastValid.UseScanCodes);
        if (!host.IsTargetForeground() && options.StartDelayMs == 0)
        {
            SetNotice(any
                ? "KeyTapLoop is in front, so there is nowhere to send keys. Set a start delay and switch to your window, or start with the shortcut from that window."
                : fromHotkey
                    ? $"{startHotkey} only starts a run while the chosen window is in front. Click that window first."
                    : "The chosen window isn\u2019t in front. Set a start delay, press Start, then switch to it before the countdown ends.");
            return;
        }

        options = options with { ReleaseBeforeStart = fromHotkey ? startHotkey.WatchedKeys() : Array.Empty<int>() };

        SettingsStore.TrySave(settingsPath, lastValid);
        if (runTarget is { IsPracticePad: true }) pad?.ResetCounters();

        var newSession = new RunSession(options, host, clock);
        session = newSession;
        activeOptions = options;
        lastRunWasHold = options.Mode == RunMode.Hold;
        runIsAnyWindow = any;
        busy = true;
        UpdateModeState();
        SetRunButtons(running: true);
        pickButton.Enabled = false;
        padButton.Enabled = false;
        adminButton.Enabled = false;
        SetNotice(null);
        uiTimer.Start();
        ShowProgress();

        NativeMethods.timeBeginPeriod(1);
        runTask = Task.Run(() => newSession.RunAsync());
        AwaitRun(newSession, runTask, runTarget);
    }

    private async void AwaitRun(RunSession s, Task<RunResult> task, TargetWindow? runTarget)
    {
        RunResult result;
        try
        {
            result = await task;
        }
        catch (Exception ex)
        {
            result = new RunResult(EndReason.InputFailed, LimitKind.None, s.Taps, s.ElapsedMs, ex.Message, false);
        }

        NativeMethods.timeEndPeriod(1);

        // Key messages are delivered after posted messages, so give the pad a moment to process the last ones.
        if (runTarget is { IsPracticePad: true }) await Task.Delay(200);
        if (IsDisposed) return;
        FinishRun(result, runTarget);
    }

    private void FinishRun(RunResult result, TargetWindow? runTarget)
    {
        uiTimer.Stop();
        session = null;
        runTask = null;
        busy = false;
        UpdateModeState();
        SetRunButtons(running: false);
        pickButton.Enabled = !picking;
        padButton.Enabled = true;
        adminButton.Enabled = true;

        if (result.Reason == EndReason.TargetClosed && runTarget is not null)
        {
            target = null;
            UpdateTargetLabel();
        }
        ShowResult(result, runTarget);
    }

    private void RestartAsAdministrator()
    {
        if (busy) { SetNotice("Stop the current run before restarting."); return; }
        SettingsStore.TrySave(settingsPath, lastValid);
        if (Elevation.TryRestartElevated(out var error)) Close();
        else SetNotice(error);
    }

    private void StopRun()
    {
        if (picking) { EndPicking(); SetNotice("Window selection cancelled."); return; }
        session?.Stop();
    }

    private void SetRunButtons(bool running)
    {
        startButton.Enabled = !running;
        stopButton.Enabled = running;
    }

    // ===================================================================================
    //  Result card
    // ===================================================================================

    private void ShowIdle()
    {
        stateLabel.Text = "Ready";
        stateLabel.ForeColor = Theme.Text;
        detailLabel.Text = "Pick where the keys go, set the key, then press Start.";
        statsLabel.Text = "";
    }

    private void ShowProgress()
    {
        if (session is null) return;
        switch (session.Phase)
        {
            case RunPhase.Countdown:
                stateLabel.Text = "Starting…";
                stateLabel.ForeColor = Theme.Accent;
                detailLabel.Text = $"Switch to the target. Beginning in {Math.Ceiling(session.CountdownRemainingMs / 1000.0):0} s.";
                statsLabel.Text = "";
                break;
            case RunPhase.Idle:
            case RunPhase.Starting:
                stateLabel.Text = "Starting…";
                stateLabel.ForeColor = Theme.Accent;
                detailLabel.Text = "Checking the target.";
                break;
            case RunPhase.Ended:
                return; // the final result is about to replace this
            default:
                stateLabel.Text = lastRunWasHold ? "Holding" : "Running";
                stateLabel.ForeColor = Theme.Accent;
                detailLabel.Text = "Switching windows ends the run.";
                statsLabel.Text = ProgressText(session);
                break;
        }
    }

    private string ProgressText(RunSession s)
    {
        var elapsed = FormatSeconds(s.ElapsedMs);
        if (activeOptions is { Mode: RunMode.Hold })
            return activeOptions.DurationMs is { } holdMs ? $"{elapsed} of {FormatSeconds(holdMs)}" : elapsed;
        var taps = activeOptions?.MaxTaps is { } max ? $"{s.Taps} of {max} taps" : $"{s.Taps} taps";
        return activeOptions?.DurationMs is { } ms ? $"{taps}  ·  {elapsed} of {FormatSeconds(ms)}" : $"{taps}  ·  {elapsed}";
    }

    private void ShowResult(RunResult result, TargetWindow? runTarget)
    {
        stateLabel.Text = result.Title;
        stateLabel.ForeColor = result.Reason switch
        {
            EndReason.Completed => Theme.Accent,
            EndReason.StoppedByUser => Theme.Text,
            EndReason.FocusChanged => Theme.Warn,
            _ => Theme.Danger,
        };

        var detail = result.Detail;
        var stats = lastRunWasHold
            ? $"Held for {FormatSeconds(result.ElapsedMs)}"
            : $"{result.Taps} {(result.Taps == 1 ? "tap" : "taps")} accepted by Windows  ·  {FormatSeconds(result.ElapsedMs)}";

        if (runTarget is { IsPracticePad: true } && pad is { IsDisposed: false } && !lastRunWasHold)
        {
            var seen = pad.TapCycles;
            stats += $"\nPractice pad saw {seen} of {result.Taps}.";
            if (seen != result.Taps) detail = (detail + " The pad did not see every tap Windows accepted.").Trim();
        }
        else if (result.Reason == EndReason.Completed || result.Taps > 0)
        {
            detail = (detail + " “Accepted” means Windows queued the keys. Check the target window to confirm it reacted.").Trim();
        }

        detailLabel.Text = detail;
        statsLabel.Text = stats;
    }

    private static string FormatSeconds(long ms)
    {
        if (ms < 60_000) return $"{ms / 1000.0:0.0} s";
        var t = TimeSpan.FromMilliseconds(ms);
        return t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}" : $"{t.Minutes}:{t.Seconds:00}";
    }

    private void SetNotice(string? text, bool warn = true)
    {
        noticeLabel.Text = text ?? "";
        noticeLabel.ForeColor = warn ? Theme.Warn : Theme.Muted;
        noticeLabel.Visible = !string.IsNullOrEmpty(text);
    }

    // ===================================================================================
    //  Shortcuts
    // ===================================================================================

    private void EditShortcuts()
    {
        if (busy) { SetNotice("Stop the current run before changing shortcuts."); return; }
        using var dialog = new ShortcutsForm(startHotkey, stopHotkey);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        var oldStart = startHotkey;
        var oldStop = stopHotkey;
        UnregisterHotkeys();
        var problems = RegisterHotkeys(dialog.Start, dialog.Stop);
        if (problems.Count == 0)
        {
            startHotkey = dialog.Start;
            stopHotkey = dialog.Stop;
            SetNotice(null);
        }
        else
        {
            UnregisterHotkeys();
            RegisterHotkeys(oldStart, oldStop);
            SetNotice(string.Join(" ", problems) + " Your previous shortcuts are still active.");
        }
        UpdateButtonLabels();
        Revalidate();
        SettingsStore.TrySave(settingsPath, lastValid);
    }

    private List<string> RegisterHotkeys(KeyChord start, KeyChord stop)
    {
        var problems = new List<string>();
        if (!Hotkeys.TryRegister(Handle, StartHotkeyId, start, out var startError)) problems.Add(startError);
        if (!Hotkeys.TryRegister(Handle, StopHotkeyId, stop, out var stopError)) problems.Add(stopError);
        return problems;
    }

    private void UnregisterHotkeys()
    {
        if (!IsHandleCreated) return;
        Hotkeys.Unregister(Handle, StartHotkeyId);
        Hotkeys.Unregister(Handle, StopHotkeyId);
    }

    // ===================================================================================
    //  Window lifecycle
    // ===================================================================================

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        foreach (var problem in RegisterHotkeys(startHotkey, stopHotkey))
            startupNotes.Add(problem + " Use the on-screen buttons or pick another shortcut.");
        if (startupNotes.Count > 0) SetNotice(string.Join(" ", startupNotes));
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == NativeMethods.WM_HOTKEY)
        {
            if (captureButton.Listening) return;
            switch (m.WParam.ToInt32())
            {
                case StartHotkeyId: StartRun(fromHotkey: true); break;
                case StopHotkeyId: StopRun(); break;
            }
            return;
        }
        base.WndProc(ref m);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);
        SettingsStore.TrySave(settingsPath, lastValid);

        // Make sure any key we are holding is released before the process goes away.
        session?.Stop();
        try { runTask?.Wait(1500); } catch (AggregateException) { }

        pickTimer.Stop();
        uiTimer.Stop();
        UnregisterHotkeys();
        watcher.Dispose();
        if (pad is { IsDisposed: false }) pad.Close();
    }
}
