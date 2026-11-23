using System.Text;

namespace KeyTapLoop.Core;

/// <summary>A key the app knows how to send, identified by its Windows virtual-key code.</summary>
public sealed record KeyInfo(int VirtualKey, string Name, bool Extended)
{
    public override string ToString() => Name;
}

/// <summary>The keys KeyTapLoop can send or use in a shortcut.</summary>
public static class KeyCatalog
{
    public const int VkTab = 0x09;
    public const int VkEscape = 0x1B;
    public const int VkF4 = 0x73;

    private static readonly List<KeyInfo> all = Build();
    private static readonly Dictionary<int, KeyInfo> byVk = all.ToDictionary(k => k.VirtualKey);
    private static readonly Dictionary<string, KeyInfo> byName = BuildNameIndex();

    public static IReadOnlyList<KeyInfo> All => all;

    public static bool TryGet(int virtualKey, out KeyInfo key) => byVk.TryGetValue(virtualKey, out key!);

    public static bool TryGet(string name, out KeyInfo key) =>
        byName.TryGetValue(name.Trim(), out key!);

    private static List<KeyInfo> Build()
    {
        var keys = new List<KeyInfo>();
        for (var c = 'A'; c <= 'Z'; c++) keys.Add(new KeyInfo(c, c.ToString(), false));
        for (var c = '0'; c <= '9'; c++) keys.Add(new KeyInfo(c, c.ToString(), false));
        keys.Add(new KeyInfo(0x20, "Space", false));
        keys.Add(new KeyInfo(0x0D, "Enter", false));
        keys.Add(new KeyInfo(VkTab, "Tab", false));
        keys.Add(new KeyInfo(VkEscape, "Escape", false));
        keys.Add(new KeyInfo(0x25, "Left", true));
        keys.Add(new KeyInfo(0x26, "Up", true));
        keys.Add(new KeyInfo(0x27, "Right", true));
        keys.Add(new KeyInfo(0x28, "Down", true));
        for (var i = 1; i <= 12; i++) keys.Add(new KeyInfo(0x6F + i, "F" + i, false));
        return keys;
    }

    private static Dictionary<string, KeyInfo> BuildNameIndex()
    {
        var index = new Dictionary<string, KeyInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var k in all) index[k.Name] = k;
        index["Esc"] = byVk[VkEscape];
        index["Return"] = byVk[0x0D];
        return index;
    }
}

/// <summary>One physical key event the app will send.</summary>
public readonly record struct KeyStep(int VirtualKey, bool Extended, string Label);

/// <summary>A key plus optional Ctrl, Alt and Shift.</summary>
public readonly record struct KeyChord(int VirtualKey, bool Ctrl, bool Alt, bool Shift)
{
    public const int VkShift = 0x10, VkControl = 0x11, VkMenu = 0x12;
    private const int VkLShift = 0xA0, VkLControl = 0xA2, VkLMenu = 0xA4;

    public bool HasModifier => Ctrl || Alt || Shift;

    public KeyInfo Key => KeyCatalog.TryGet(VirtualKey, out var k) ? k : new KeyInfo(VirtualKey, $"0x{VirtualKey:X2}", false);

    /// <summary>Key events in the order they are pressed: modifiers first, then the main key.</summary>
    public IReadOnlyList<KeyStep> PressOrder()
    {
        var steps = new List<KeyStep>(4);
        if (Ctrl) steps.Add(new KeyStep(VkLControl, false, "Ctrl"));
        if (Alt) steps.Add(new KeyStep(VkLMenu, false, "Alt"));
        if (Shift) steps.Add(new KeyStep(VkLShift, false, "Shift"));
        steps.Add(new KeyStep(VirtualKey, Key.Extended, Key.Name));
        return steps;
    }

    /// <summary>Generic virtual keys used to ask Windows whether the user is holding any of these.</summary>
    public IReadOnlyList<int> WatchedKeys()
    {
        var keys = new List<int>(4);
        if (Ctrl) keys.Add(VkControl);
        if (Alt) keys.Add(VkMenu);
        if (Shift) keys.Add(VkShift);
        keys.Add(VirtualKey);
        return keys;
    }

    public override string ToString()
    {
        var sb = new StringBuilder();
        if (Ctrl) sb.Append("Ctrl + ");
        if (Alt) sb.Append("Alt + ");
        if (Shift) sb.Append("Shift + ");
        sb.Append(Key.Name);
        return sb.ToString();
    }

    /// <summary>Compact form used in the settings file, e.g. <c>Ctrl+F1</c>.</summary>
    public string ToStorageString() => ToString().Replace(" ", "");

    public static bool TryParse(string? text, out KeyChord chord)
    {
        chord = default;
        if (string.IsNullOrWhiteSpace(text)) return false;
        bool ctrl = false, alt = false, shift = false;
        KeyInfo? main = null;
        foreach (var raw in text.Split('+', StringSplitOptions.TrimEntries))
        {
            if (raw.Length == 0) return false;
            if (raw.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) || raw.Equals("Control", StringComparison.OrdinalIgnoreCase)) ctrl = true;
            else if (raw.Equals("Alt", StringComparison.OrdinalIgnoreCase)) alt = true;
            else if (raw.Equals("Shift", StringComparison.OrdinalIgnoreCase)) shift = true;
            else if (main is null && KeyCatalog.TryGet(raw, out var k)) main = k;
            else return false;
        }
        if (main is null) return false;
        chord = new KeyChord(main.VirtualKey, ctrl, alt, shift);
        return true;
    }
}

/// <summary>Shortcuts that Windows handles itself or that would end or damage a run.</summary>
public static class ReservedChords
{
    public static bool IsReserved(KeyChord c, out string reason)
    {
        if (c.Alt && c.VirtualKey == KeyCatalog.VkTab) { reason = "Alt+Tab switches windows, which ends a run."; return true; }
        if (c.Alt && c.VirtualKey == KeyCatalog.VkF4) { reason = "Alt+F4 closes the active window."; return true; }
        if ((c.Alt || c.Ctrl) && c.VirtualKey == KeyCatalog.VkEscape) { reason = "This Escape combination is reserved by Windows."; return true; }
        reason = "";
        return false;
    }
}
