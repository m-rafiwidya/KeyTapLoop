using KeyTapLoop.Core;
using Xunit;

namespace KeyTapLoop.Core.Tests;

public class ValidationTests
{
    private static RunInput Valid() => new()
    {
        Output = new KeyChord('F', false, false, false),
        Mode = RunMode.Repeat,
        Period = "200",
        KeyDown = "40",
        CountEnabled = true,
        Count = "20",
        DurationEnabled = false,
        DurationSeconds = "10",
        DelaySeconds = "3",
        StartHotkey = AppSettings.DefaultStartHotkey,
        StopHotkey = AppSettings.DefaultStopHotkey,
        ToggleHotkey = AppSettings.DefaultToggleHotkey,
    };

    [Fact]
    public void Defaults_are_valid_and_map_to_the_documented_values()
    {
        var result = RunValidator.Validate(Valid());
        Assert.True(result.IsValid);
        var o = result.Options!;
        Assert.Equal(200, o.PeriodMs);
        Assert.Equal(40, o.KeyDownMs);
        Assert.Equal(20, o.MaxTaps);
        Assert.Null(o.DurationMs);
        Assert.Equal(3000, o.StartDelayMs);
    }

    [Fact]
    public void Period_must_exceed_the_key_down_time()
    {
        var input = Valid();
        input.Period = "40";
        var result = RunValidator.Validate(input);
        Assert.False(result.IsValid);
        Assert.Contains(Field.Period, result.Errors.Keys);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("-5")]
    [InlineData("1.5")]
    [InlineData("0")]
    public void Bad_period_text_is_reported_on_the_period_field(string text)
    {
        var input = Valid();
        input.Period = text;
        var result = RunValidator.Validate(input);
        Assert.Contains(Field.Period, result.Errors.Keys);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Repeat_with_no_limit_is_allowed_and_runs_until_stopped()
    {
        var input = Valid();
        input.CountEnabled = false;
        input.DurationEnabled = false;
        var result = RunValidator.Validate(input);
        Assert.True(result.IsValid);
        Assert.Null(result.Options!.MaxTaps);
        Assert.Null(result.Options.DurationMs);
    }

    [Fact]
    public void A_switched_on_limit_still_has_to_be_valid()
    {
        var input = Valid();
        input.CountEnabled = true;
        input.Count = "0";
        Assert.Contains(Field.Count, RunValidator.Validate(input).Errors.Keys);

        input.Count = "5";
        input.DurationEnabled = true;
        input.DurationSeconds = "";
        Assert.Contains(Field.Duration, RunValidator.Validate(input).Errors.Keys);
    }

    [Fact]
    public void A_duration_alone_is_enough_and_accepts_decimals_with_either_separator()
    {
        var input = Valid();
        input.CountEnabled = false;
        input.DurationEnabled = true;
        input.DurationSeconds = "2,5";
        var result = RunValidator.Validate(input);
        Assert.True(result.IsValid);
        Assert.Equal(2500, result.Options!.DurationMs);
        Assert.Null(result.Options.MaxTaps);
    }

    [Fact]
    public void Hold_ignores_repeat_fields()
    {
        var input = Valid();
        input.Mode = RunMode.Hold;
        input.Period = "garbage";
        input.KeyDown = "";
        input.Count = "";
        input.CountEnabled = true;
        input.DurationEnabled = false;
        input.DurationSeconds = "";

        var result = RunValidator.Validate(input);

        Assert.True(result.IsValid);                     // holds until stopped
        Assert.Null(result.Options!.DurationMs);
        Assert.Null(result.Options.MaxTaps);
    }

    [Fact]
    public void Hold_with_a_time_limit_needs_a_valid_time()
    {
        var input = Valid();
        input.Mode = RunMode.Hold;
        input.DurationEnabled = true;
        input.DurationSeconds = "nope";
        Assert.Equal(new[] { Field.Duration }, RunValidator.Validate(input).Errors.Keys.ToArray());

        input.DurationSeconds = "1.5";
        Assert.Equal(1500, RunValidator.Validate(input).Options!.DurationMs);
    }

    [Fact]
    public void Output_cannot_match_the_apps_own_shortcuts()
    {
        var input = Valid();
        input.Output = AppSettings.DefaultStartHotkey;
        Assert.Contains(Field.Output, RunValidator.Validate(input).Errors.Keys);
    }

    [Theory]
    [InlineData("Alt+Tab")]
    [InlineData("Alt+F4")]
    [InlineData("Ctrl+Escape")]
    public void Windows_reserved_output_is_rejected(string chord)
    {
        Assert.True(KeyChord.TryParse(chord, out var parsed));
        var input = Valid();
        input.Output = parsed;
        Assert.Contains(Field.Output, RunValidator.Validate(input).Errors.Keys);
    }

    [Fact]
    public void Tab_and_Escape_are_valid_plain_outputs()
    {
        foreach (var name in new[] { "Tab", "Escape" })
        {
            Assert.True(KeyChord.TryParse(name, out var chord));
            var input = Valid();
            input.Output = chord;
            Assert.True(RunValidator.Validate(input).IsValid, name);
        }
    }

    [Fact]
    public void Missing_output_is_reported()
    {
        var input = Valid();
        input.Output = null;
        Assert.Contains(Field.Output, RunValidator.Validate(input).Errors.Keys);
    }

    [Fact]
    public void Delay_is_limited()
    {
        var input = Valid();
        input.DelaySeconds = "61";
        Assert.Contains(Field.Delay, RunValidator.Validate(input).Errors.Keys);
    }

    [Theory]
    [InlineData("F1", "Ctrl+F2", "Ctrl+F3", false)]          // no modifier on Start
    [InlineData("Ctrl+F1", "Ctrl+F1", "Ctrl+F3", false)]     // Start and Stop the same
    [InlineData("Ctrl+F1", "Ctrl+F2", "Ctrl+F2", false)]     // Stop and Toggle the same
    [InlineData("Ctrl+F1", "Ctrl+F2", "Ctrl+F1", false)]     // Start and Toggle the same
    [InlineData("Alt+Tab", "Ctrl+F2", "Ctrl+F3", false)]     // reserved
    [InlineData("Ctrl+Tab", "Ctrl+F2", "Ctrl+F3", false)]    // Tab not allowed
    [InlineData("Ctrl+F1", "Ctrl+F2", "F3", false)]          // Toggle needs a modifier too
    [InlineData("Ctrl+F1", "Ctrl+F2", "Ctrl+F3", true)]
    [InlineData("Ctrl+Shift+S", "Ctrl+Shift+X", "Ctrl+Shift+T", true)]
    public void Hotkey_sets_are_checked(string start, string stop, string toggle, bool ok)
    {
        Assert.True(KeyChord.TryParse(start, out var a));
        Assert.True(KeyChord.TryParse(stop, out var b));
        Assert.True(KeyChord.TryParse(toggle, out var c));
        Assert.Equal(ok, RunValidator.ValidateHotkeys(a, b, c) is null);
    }

    [Fact]
    public void Output_cannot_match_the_toggle_shortcut_either()
    {
        var input = Valid();
        input.Output = AppSettings.DefaultToggleHotkey;
        Assert.Contains(Field.Output, RunValidator.Validate(input).Errors.Keys);
    }
}

public class KeyChordTests
{
    [Theory]
    [InlineData("Ctrl+F1", "Ctrl + F1")]
    [InlineData("ctrl + shift + a", "Ctrl + Shift + A")]
    [InlineData("Esc", "Escape")]
    [InlineData("Alt+Left", "Alt + Left")]
    public void Chords_parse_and_display_readably(string text, string display)
    {
        Assert.True(KeyChord.TryParse(text, out var chord));
        Assert.Equal(display, chord.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("Ctrl+")]
    [InlineData("Ctrl+Shift")]
    [InlineData("A+B")]
    [InlineData("Banana")]
    public void Garbage_does_not_parse(string text) => Assert.False(KeyChord.TryParse(text, out _));

    [Fact]
    public void Storage_form_round_trips()
    {
        var chord = new KeyChord(0x71, true, true, false);
        Assert.True(KeyChord.TryParse(chord.ToStorageString(), out var back));
        Assert.Equal(chord, back);
    }

    [Fact]
    public void Arrow_keys_are_sent_as_extended_keys()
    {
        Assert.True(KeyChord.TryParse("Left", out var chord));
        Assert.True(chord.PressOrder().Last().Extended);
    }

    [Fact]
    public void Every_listed_key_has_a_unique_code_and_name()
    {
        Assert.Equal(KeyCatalog.All.Count, KeyCatalog.All.Select(k => k.VirtualKey).Distinct().Count());
        Assert.Equal(KeyCatalog.All.Count, KeyCatalog.All.Select(k => k.Name).Distinct().Count());
    }
}

public sealed class SettingsTests : IDisposable
{
    private readonly string dir = Path.Combine(Path.GetTempPath(), "keytaploop-tests-" + Guid.NewGuid().ToString("N"));
    private string PathFor(string name) => Path.Combine(dir, name);

    public SettingsTests() => Directory.CreateDirectory(dir);
    public void Dispose() { try { Directory.Delete(dir, true); } catch (IOException) { } }

    [Fact]
    public void Missing_file_gives_defaults_without_a_warning()
    {
        var result = SettingsStore.Load(PathFor("nope.json"));
        Assert.Null(result.Warning);
        Assert.Equal(200, result.Settings.PeriodMs);
        Assert.Equal("Ctrl+F1", result.Settings.StartHotkey);
    }

    [Fact]
    public void Settings_round_trip()
    {
        var path = PathFor("s.json");
        var saved = new AppSettings { Output = "Ctrl+Shift+A", Mode = "Hold", PeriodMs = 500, KeyDownMs = 30, MaxTaps = 7, DurationEnabled = true, DurationMs = 4000, StartDelaySeconds = 0, StartHotkey = "Ctrl+Alt+F5", StopHotkey = "Ctrl+Alt+F6" };
        Assert.True(SettingsStore.TrySave(path, saved));

        var loaded = SettingsStore.Load(path);
        Assert.Null(loaded.Warning);
        Assert.Equal("Ctrl+Shift+A", loaded.Settings.Output);
        Assert.Equal(RunMode.Hold, loaded.Settings.RunMode);
        Assert.Equal(500, loaded.Settings.PeriodMs);
        Assert.Equal(7, loaded.Settings.MaxTaps);
        Assert.Equal("Ctrl+Alt+F5", loaded.Settings.StartHotkey);
    }

    [Fact]
    public void Corrupt_json_falls_back_to_defaults_with_a_warning()
    {
        var path = PathFor("bad.json");
        File.WriteAllText(path, "{ not json");
        var result = SettingsStore.Load(path);
        Assert.NotNull(result.Warning);
        Assert.Equal(200, result.Settings.PeriodMs);
    }

    [Fact]
    public void Invalid_values_are_replaced_one_by_one()
    {
        var path = PathFor("odd.json");
        File.WriteAllText(path, """{ "Output": "Banana", "PeriodMs": 50, "KeyDownMs": 90, "MaxTaps": 12, "StartHotkey": "F1", "StopHotkey": "F2" }""");
        var result = SettingsStore.Load(path);

        Assert.NotNull(result.Warning);
        Assert.Equal("F", result.Settings.Output);                   // bad key replaced
        Assert.Equal(200, result.Settings.PeriodMs);                 // period no longer exceeds key-down: both reset
        Assert.Equal(40, result.Settings.KeyDownMs);
        Assert.Equal(12, result.Settings.MaxTaps);                   // valid value kept
        Assert.Equal("Ctrl+F1", result.Settings.StartHotkey);        // hotkeys without a modifier rejected
    }

    [Fact]
    public void Empty_file_is_treated_as_invalid()
    {
        var path = PathFor("empty.json");
        File.WriteAllText(path, "");
        Assert.NotNull(SettingsStore.Load(path).Warning);
    }

    [Fact]
    public void An_older_settings_file_without_a_toggle_keeps_the_users_own_shortcuts()
    {
        var path = PathFor("old.json");
        File.WriteAllText(path, """{ "StartHotkey": "Ctrl+Alt+F5", "StopHotkey": "Ctrl+Alt+F6" }""");
        var result = SettingsStore.Load(path);
        Assert.Null(result.Warning);
        Assert.Equal("Ctrl+Alt+F5", result.Settings.StartHotkey);
        Assert.Equal("Ctrl+Alt+F6", result.Settings.StopHotkey);
        Assert.Equal("Ctrl+F3", result.Settings.ToggleHotkey);
    }

    [Fact]
    public void Sound_and_theme_preferences_round_trip_and_bad_themes_are_replaced()
    {
        Assert.True(new AppSettings().SoundCues);
        Assert.Equal("Auto", new AppSettings().Theme);

        var path = PathFor("look.json");
        SettingsStore.TrySave(path, new AppSettings { SoundCues = false, Theme = "Dark" });
        var loaded = SettingsStore.Load(path).Settings;
        Assert.False(loaded.SoundCues);
        Assert.Equal("Dark", loaded.Theme);

        File.WriteAllText(path, """{ "Theme": "Neon" }""");
        var bad = SettingsStore.Load(path);
        Assert.NotNull(bad.Warning);
        Assert.Equal("Auto", bad.Settings.Theme);
    }

    [Fact]
    public void The_scan_code_preference_is_remembered_and_defaults_on()
    {
        Assert.True(new AppSettings().UseScanCodes);
        var path = PathFor("scan.json");
        SettingsStore.TrySave(path, new AppSettings { UseScanCodes = false });
        Assert.False(SettingsStore.Load(path).Settings.UseScanCodes);
    }

    [Fact]
    public void Saved_settings_contain_no_window_or_run_state()
    {
        var path = PathFor("clean.json");
        SettingsStore.TrySave(path, new AppSettings());
        var text = File.ReadAllText(path).ToLowerInvariant();
        Assert.DoesNotContain("hwnd", text);
        Assert.DoesNotContain("handle", text);
        Assert.DoesNotContain("target", text);
        Assert.DoesNotContain("running", text);
    }
}
