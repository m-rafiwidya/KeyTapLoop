using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using KeyTapLoop.Core;

namespace KeyTapLoop.Ui;

/// <summary>One quiet palette: warm neutrals, a single green accent, flat controls, hairline borders.</summary>
internal static class Theme
{
    public static readonly Color Background = Color.FromArgb(0xF6, 0xF5, 0xF2);
    public static readonly Color Surface = Color.White;
    public static readonly Color Line = Color.FromArgb(0xDD, 0xDA, 0xD2);
    public static readonly Color Text = Color.FromArgb(0x1F, 0x1E, 0x1B);
    public static readonly Color Muted = Color.FromArgb(0x6B, 0x68, 0x60);
    public static readonly Color Accent = Color.FromArgb(0x1E, 0x6B, 0x55);
    public static readonly Color AccentHover = Color.FromArgb(0x18, 0x58, 0x45);
    public static readonly Color AccentSoft = Color.FromArgb(0xE3, 0xEF, 0xEA);
    public static readonly Color Danger = Color.FromArgb(0xB0, 0x35, 0x28);
    public static readonly Color Warn = Color.FromArgb(0x8F, 0x5F, 0x00);
    public static readonly Color Disabled = Color.FromArgb(0xA6, 0xA3, 0x9B);

    private static readonly string Family = PickFamily();

    public static Font Body(float size = 9.5f, FontStyle style = FontStyle.Regular) => new(Family, size, style);
    public static Font Title => Body(17f, FontStyle.Bold);
    public static Font Caption => Body(8.25f, FontStyle.Bold);
    public static Font Mono => new("Cascadia Mono", 9f);

    private static string PickFamily()
    {
        var installed = FontFamily.Families.Select(f => f.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var name in new[] { "Segoe UI Variable Text", "Segoe UI" })
            if (installed.Contains(name)) return name;
        return SystemFonts.DefaultFont.FontFamily.Name;
    }

    // ---- Factories ------------------------------------------------------------------------

    public static Label Label(string text, bool muted = false, float size = 9.5f, FontStyle style = FontStyle.Regular) => new()
    {
        Text = text,
        AutoSize = true,
        ForeColor = muted ? Muted : Text,
        BackColor = Color.Transparent,
        Font = Body(size, style),
        Margin = Padding.Empty,
    };

    /// <summary>Small uppercase section heading.</summary>
    public static Label Section(string text) => new()
    {
        Text = text.ToUpperInvariant(),
        AutoSize = true,
        ForeColor = Muted,
        BackColor = Color.Transparent,
        Font = Caption,
        Margin = new Padding(0, 0, 0, 8),
    };

    /// <summary>Red text that sits right under the control it describes. Hidden until there is something to say.</summary>
    public static Label ErrorLabel(int maxWidth) => new()
    {
        AutoSize = true,
        MaximumSize = new Size(maxWidth, 0),
        ForeColor = Danger,
        BackColor = Color.Transparent,
        Font = Body(8.75f),
        Margin = new Padding(0, 4, 0, 0),
        Visible = false,
    };

    public static void SetError(Label label, string? message)
    {
        label.Text = message ?? "";
        label.Visible = !string.IsNullOrEmpty(message);
    }

    public static void StyleButton(Button b, bool primary = false, bool quiet = false)
    {
        b.FlatStyle = FlatStyle.Flat;
        b.UseVisualStyleBackColor = false;
        b.Font = Body(9.5f, primary ? FontStyle.Bold : FontStyle.Regular);
        b.Cursor = Cursors.Hand;
        b.Margin = Padding.Empty;
        b.TextAlign = ContentAlignment.MiddleCenter;
        var fa = b.FlatAppearance;
        if (primary)
        {
            b.BackColor = Accent;
            b.ForeColor = Color.White;
            fa.BorderSize = 0;
            fa.MouseOverBackColor = AccentHover;
            fa.MouseDownBackColor = AccentHover;
        }
        else if (quiet)
        {
            b.BackColor = Color.Transparent;
            b.ForeColor = Accent;
            fa.BorderSize = 0;
            fa.MouseOverBackColor = AccentSoft;
            fa.MouseDownBackColor = AccentSoft;
        }
        else
        {
            b.BackColor = Surface;
            b.ForeColor = Text;
            fa.BorderSize = 1;
            fa.BorderColor = Line;
            fa.MouseOverBackColor = Color.FromArgb(0xF0, 0xEE, 0xE9);
            fa.MouseDownBackColor = Color.FromArgb(0xE8, 0xE5, 0xDE);
        }
    }

    /// <summary>A button-shaped toggle used for modifier keys and the Repeat/Hold switch.</summary>
    public static void StyleToggle(ButtonBase b)
    {
        switch (b)
        {
            case CheckBox c: c.Appearance = Appearance.Button; break;
            case RadioButton r: r.Appearance = Appearance.Button; break;
        }
        b.FlatStyle = FlatStyle.Flat;
        b.UseVisualStyleBackColor = false;
        b.Font = Body(9.5f);
        b.Cursor = Cursors.Hand;
        b.Margin = Padding.Empty;
        b.TextAlign = ContentAlignment.MiddleCenter;
        b.BackColor = Surface;
        b.ForeColor = Text;
        var fa = b.FlatAppearance;
        fa.BorderSize = 1;
        fa.BorderColor = Line;
        fa.CheckedBackColor = AccentSoft;
        fa.MouseOverBackColor = Color.FromArgb(0xF0, 0xEE, 0xE9);
        fa.MouseDownBackColor = AccentSoft;
    }

}

/// <summary>A single-line input with a hairline border that turns green on focus and red on error.</summary>
internal sealed class TextField : Panel
{
    private readonly TextBox box;
    private bool hasError;

    public event EventHandler? ValueChanged;

    public TextField(int width, string accessibleName)
    {
        Size = new Size(width, 32);
        Margin = Padding.Empty;
        BackColor = Theme.Surface;
        Padding = new Padding(9, 7, 9, 0);
        AccessibleName = accessibleName;

        box = new TextBox
        {
            BorderStyle = BorderStyle.None,
            Dock = DockStyle.Fill,
            BackColor = Theme.Surface,
            ForeColor = Theme.Text,
            Font = Theme.Body(),
            AccessibleName = accessibleName,
            MaxLength = 12,
        };
        box.TextChanged += (_, _) => ValueChanged?.Invoke(this, EventArgs.Empty);
        box.GotFocus += (_, _) => Invalidate();
        box.LostFocus += (_, _) => Invalidate();
        box.Enter += (_, _) => box.SelectAll();
        Controls.Add(box);
        SetStyle(ControlStyles.ResizeRedraw, true);
    }

    [AllowNull]
    public override string Text { get => box.Text; set => box.Text = value ?? ""; }

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool HasError
    {
        get => hasError;
        set { if (hasError != value) { hasError = value; Invalidate(); } }
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        BackColor = Enabled ? Theme.Surface : Theme.Background;
        box.BackColor = BackColor;
        box.ForeColor = Enabled ? Theme.Text : Theme.Disabled;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var color = hasError ? Theme.Danger : box.Focused ? Theme.Accent : Theme.Line;
        using var pen = new Pen(color, 1f);
        e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
    }
}

/// <summary>
/// A button that, when clicked, waits for the next key combination and reports it.
/// Escape cancels; Tab cancels and moves on, so keyboard users are never trapped.
/// </summary>
internal sealed class CaptureButton : Button
{
    private string idleText;
    private long ignoreClicksUntil;

    public bool Listening { get; private set; }
    public event Action<KeyChord>? Captured;

    public CaptureButton(string idleText)
    {
        this.idleText = idleText;
        Text = idleText;
        Theme.StyleButton(this);
        Click += (_, _) =>
        {
            if (Environment.TickCount64 < ignoreClicksUntil) return;
            if (Listening) StopListening(); else StartListening();
        };
        LostFocus += (_, _) => StopListening();
    }

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string ShownText
    {
        set { idleText = value; if (!Listening) Text = value; }
    }

    private void StartListening()
    {
        Listening = true;
        Text = "Press keys…";
        ForeColor = Theme.Accent;
    }

    public void StopListening()
    {
        if (!Listening) return;
        Listening = false;
        Text = idleText;
        ForeColor = Theme.Text;
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (!Listening) return base.ProcessCmdKey(ref msg, keyData);

        var key = keyData & Keys.KeyCode;
        var noModifiers = (keyData & Keys.Modifiers) == 0;

        if (key is Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.LWin or Keys.RWin)
            return true; // a modifier on its own: keep waiting for the main key

        if (key == Keys.Escape && noModifiers) { StopListening(); return true; }

        if (key == Keys.Tab)
        {
            StopListening();
            return base.ProcessCmdKey(ref msg, keyData); // let Tab move focus as usual
        }

        if (!KeyCatalog.TryGet((int)key, out _)) return true; // unsupported key: ignore it

        var chord = new KeyChord((int)key,
            (keyData & Keys.Control) != 0, (keyData & Keys.Alt) != 0, (keyData & Keys.Shift) != 0);
        ignoreClicksUntil = Environment.TickCount64 + 300; // Space/Enter key-up would otherwise "click" us again
        StopListening();
        Captured?.Invoke(chord);
        return true;
    }
}
