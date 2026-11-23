using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace KeyTapLoop.Ui;

/// <summary>Every colour the app uses. Each slot has a different value in each palette, so one can be swapped for the other.</summary>
internal sealed record Palette(
    Color Background, Color Surface, Color Line, Color Text, Color Muted,
    Color Accent, Color AccentHover, Color AccentSoft, Color OnAccent,
    Color Danger, Color Warn, Color Disabled, Color Hover, Color Press)
{
    public IEnumerable<Color> All => new[]
    {
        Background, Surface, Line, Text, Muted, Accent, AccentHover, AccentSoft, OnAccent, Danger, Warn, Disabled, Hover, Press,
    };
}

/// <summary>
/// One quiet palette in a light and a dark version: neutrals, a single green accent, flat controls,
/// hairline borders. Switching themes recolours every open window in place.
/// </summary>
internal static class Theme
{
    private static Color Hex(int rgb) => Color.FromArgb((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);

    public static readonly Palette Light = new(
        Background: Hex(0xF6F5F2), Surface: Hex(0xFFFFFF), Line: Hex(0xDDDAD2), Text: Hex(0x1F1E1B), Muted: Hex(0x6B6860),
        Accent: Hex(0x1E6B55), AccentHover: Hex(0x185845), AccentSoft: Hex(0xE3EFEA), OnAccent: Hex(0xFCFCFA),
        Danger: Hex(0xB03528), Warn: Hex(0x8F5F00), Disabled: Hex(0xA6A39B), Hover: Hex(0xF0EEE9), Press: Hex(0xE8E5DE));

    public static readonly Palette Dark = new(
        Background: Hex(0x17181A), Surface: Hex(0x222426), Line: Hex(0x383B3F), Text: Hex(0xE8E6E1), Muted: Hex(0x9C9990),
        Accent: Hex(0x4FB08E), AccentHover: Hex(0x63C3A1), AccentSoft: Hex(0x213A31), OnAccent: Hex(0x0C1612),
        Danger: Hex(0xF0786A), Warn: Hex(0xE0A93B), Disabled: Hex(0x5E5D58), Hover: Hex(0x2C2F32), Press: Hex(0x36393D));

    public static Palette Current { get; private set; } = Light;
    public static bool IsDark => ReferenceEquals(Current, Dark);

    public static Color Background => Current.Background;
    public static Color Surface => Current.Surface;
    public static Color Line => Current.Line;
    public static Color Text => Current.Text;
    public static Color Muted => Current.Muted;
    public static Color Accent => Current.Accent;
    public static Color AccentHover => Current.AccentHover;
    public static Color AccentSoft => Current.AccentSoft;
    public static Color OnAccent => Current.OnAccent;
    public static Color Danger => Current.Danger;
    public static Color Warn => Current.Warn;
    public static Color Disabled => Current.Disabled;

    private static readonly string Family = PickFamily();

    public static Font Body(float size = 9.5f, FontStyle style = FontStyle.Regular) => new(Family, size, style);
    public static Font Caption => Body(8.25f, FontStyle.Bold);

    private static string PickFamily()
    {
        var installed = FontFamily.Families.Select(f => f.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var name in new[] { "Segoe UI Variable Text", "Segoe UI" })
            if (installed.Contains(name)) return name;
        return SystemFonts.DefaultFont.FontFamily.Name;
    }

    // ---- Choosing and switching --------------------------------------------------------------

    /// <summary>True if Windows is set to dark mode for apps.</summary>
    public static bool SystemPrefersDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int light && light == 0;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Resolves a saved choice (Auto, Light or Dark) to a palette. High-contrast mode always gets the light one.</summary>
    public static Palette For(string mode) =>
        SystemInformation.HighContrast ? Light
        : mode == "Dark" ? Dark
        : mode == "Light" ? Light
        : SystemPrefersDark() ? Dark : Light;

    /// <summary>Picks the palette before any window exists.</summary>
    public static void Choose(string mode) => Current = For(mode);

    /// <summary>Makes <paramref name="to"/> current and recolours every control under the given windows.</summary>
    public static void Switch(Palette to, params Control[] roots)
    {
        var from = Current;
        if (ReferenceEquals(from, to)) return;
        Current = to;

        var map = new Dictionary<int, Color>();
        foreach (var (a, b) in from.All.Zip(to.All)) map[a.ToArgb()] = b;
        foreach (var root in roots) Recolor(root, map);
        foreach (var root in roots) if (root is Form f) StyleWindow(f);
    }

    private static void Recolor(Control c, Dictionary<int, Color> map)
    {
        if (map.TryGetValue(c.BackColor.ToArgb(), out var back)) c.BackColor = back;
        if (map.TryGetValue(c.ForeColor.ToArgb(), out var fore)) c.ForeColor = fore;

        if (c is ButtonBase button)
        {
            var fa = button.FlatAppearance;
            if (map.TryGetValue(fa.BorderColor.ToArgb(), out var border)) fa.BorderColor = border;
            if (map.TryGetValue(fa.MouseOverBackColor.ToArgb(), out var over)) fa.MouseOverBackColor = over;
            if (map.TryGetValue(fa.MouseDownBackColor.ToArgb(), out var down)) fa.MouseDownBackColor = down;
            if (map.TryGetValue(fa.CheckedBackColor.ToArgb(), out var on)) fa.CheckedBackColor = on;
        }

        StyleNative(c);
        c.Invalidate();
        foreach (Control child in c.Controls) Recolor(child, map);
    }

    // ---- Parts Windows draws itself --------------------------------------------------------------

    private const int DwmUseImmersiveDarkMode = 20;
    private const int DwmUseImmersiveDarkModeBefore20H1 = 19;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr window, string? appName, string? idList);

    /// <summary>Dark title bar for dark mode. Call once the window handle exists.</summary>
    public static void StyleWindow(Form form)
    {
        if (!form.IsHandleCreated) return;
        var value = IsDark ? 1 : 0;
        if (DwmSetWindowAttribute(form.Handle, DwmUseImmersiveDarkMode, ref value, sizeof(int)) != 0)
            DwmSetWindowAttribute(form.Handle, DwmUseImmersiveDarkModeBefore20H1, ref value, sizeof(int));
    }

    /// <summary>Dark scrollbars on the text box and list that have them.</summary>
    public static void StyleNative(Control c)
    {
        if (c is not (TextBox or ListBox) || !c.IsHandleCreated) return;
        SetWindowTheme(c.Handle, IsDark ? "DarkMode_Explorer" : "Explorer", null);
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
            b.ForeColor = Current.OnAccent;
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
            fa.MouseOverBackColor = Current.Hover;
            fa.MouseDownBackColor = Current.Press;
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
        fa.MouseOverBackColor = Current.Hover;
        fa.MouseDownBackColor = AccentSoft;
    }
}
