using System.Text.Json;
using System.Text.Json.Serialization;

namespace KeyTapLoop.Core;

/// <summary>What is remembered between launches. No window handles, no running state.</summary>
public sealed class AppSettings
{
    public static readonly KeyChord DefaultStartHotkey = new(0x70, true, false, false); // Ctrl+F1
    public static readonly KeyChord DefaultStopHotkey = new(0x71, true, false, false);  // Ctrl+F2
    public static readonly KeyChord DefaultToggleHotkey = new(0x72, true, false, false); // Ctrl+F3

    public int Version { get; set; } = 1;
    public string Output { get; set; } = "F";
    public string Mode { get; set; } = nameof(RunMode.Repeat);
    public int PeriodMs { get; set; } = RunOptions.DefaultPeriodMs;
    public int KeyDownMs { get; set; } = RunOptions.DefaultKeyDownMs;
    public bool CountEnabled { get; set; } = true;
    public int MaxTaps { get; set; } = RunOptions.DefaultMaxTaps;
    public bool DurationEnabled { get; set; }
    public int DurationMs { get; set; } = RunOptions.DefaultDurationMs;
    public int StartDelaySeconds { get; set; } = RunOptions.DefaultStartDelaySeconds;

    /// <summary>Send hardware scan codes instead of virtual keys. Games that read the keyboard directly usually need this.</summary>
    public bool UseScanCodes { get; set; } = true;
    public string StartHotkey { get; set; } = DefaultStartHotkey.ToStorageString();
    public string StopHotkey { get; set; } = DefaultStopHotkey.ToStorageString();
    public string ToggleHotkey { get; set; } = DefaultToggleHotkey.ToStorageString();

    /// <summary>Short tones when a run starts, ends, or is cut short.</summary>
    public bool SoundCues { get; set; } = true;

    /// <summary>Auto follows the Windows light or dark setting.</summary>
    public string Theme { get; set; } = "Auto";

    public AppSettings Clone() => (AppSettings)MemberwiseClone();

    [JsonIgnore] public KeyChord OutputChord => KeyChord.TryParse(Output, out var c) ? c : new KeyChord('F', false, false, false);
    [JsonIgnore] public KeyChord StartChord => KeyChord.TryParse(StartHotkey, out var c) ? c : DefaultStartHotkey;
    [JsonIgnore] public KeyChord StopChord => KeyChord.TryParse(StopHotkey, out var c) ? c : DefaultStopHotkey;
    [JsonIgnore] public KeyChord ToggleChord => KeyChord.TryParse(ToggleHotkey, out var c) ? c : DefaultToggleHotkey;
    [JsonIgnore] public RunMode RunMode => Enum.TryParse<RunMode>(Mode, out var m) ? m : RunMode.Repeat;

    /// <summary>Returns a copy where every invalid field has been replaced by its default.
    /// <paramref name="changed"/> is true if anything had to be replaced.</summary>
    public AppSettings Sanitized(out bool changed)
    {
        var fixedSettings = new AppSettings();
        changed = false;

        if (KeyChord.TryParse(Output, out var output) && !ReservedChords.IsReserved(output, out _)) fixedSettings.Output = output.ToStorageString();
        else changed = true;

        if (Enum.TryParse<RunMode>(Mode, out var mode) && Enum.IsDefined(mode)) fixedSettings.Mode = mode.ToString();
        else changed = true;

        if (PeriodMs is >= RunValidator.MinPeriodMs and <= RunValidator.MaxPeriodMs) fixedSettings.PeriodMs = PeriodMs; else changed = true;
        if (KeyDownMs is >= 1 and <= RunValidator.MaxKeyDownMs) fixedSettings.KeyDownMs = KeyDownMs; else changed = true;
        if (fixedSettings.KeyDownMs >= fixedSettings.PeriodMs)
        {
            fixedSettings.PeriodMs = RunOptions.DefaultPeriodMs;
            fixedSettings.KeyDownMs = RunOptions.DefaultKeyDownMs;
            changed = true;
        }

        if (MaxTaps is >= 1 and <= RunValidator.MaxTaps) fixedSettings.MaxTaps = MaxTaps; else changed = true;
        if (DurationMs is >= 100 and <= 86_400_000) fixedSettings.DurationMs = DurationMs; else changed = true;
        if (StartDelaySeconds is >= 0 and <= RunValidator.MaxDelaySeconds) fixedSettings.StartDelaySeconds = StartDelaySeconds; else changed = true;

        fixedSettings.CountEnabled = CountEnabled;
        fixedSettings.DurationEnabled = DurationEnabled;
        fixedSettings.UseScanCodes = UseScanCodes;

        fixedSettings.SoundCues = SoundCues;
        if (Theme is "Auto" or "Light" or "Dark") fixedSettings.Theme = Theme; else changed = true;

        var hasStart = KeyChord.TryParse(StartHotkey, out var start);
        var hasStop = KeyChord.TryParse(StopHotkey, out var stop);
        var hasToggle = KeyChord.TryParse(ToggleHotkey, out var toggle);
        if (hasStart && hasStop && hasToggle && RunValidator.ValidateHotkeys(start, stop, toggle) is null)
        {
            fixedSettings.StartHotkey = start.ToStorageString();
            fixedSettings.StopHotkey = stop.ToStorageString();
            fixedSettings.ToggleHotkey = toggle.ToStorageString();
        }
        else if (!hasToggle && hasStart && hasStop && RunValidator.ValidateHotkeys(start, stop, AppSettings.DefaultToggleHotkey) is null)
        {
            // A settings file from before the toggle shortcut existed: keep the user's own Start and Stop.
            fixedSettings.StartHotkey = start.ToStorageString();
            fixedSettings.StopHotkey = stop.ToStorageString();
        }
        else changed = true;

        return fixedSettings;
    }
}

public sealed class SettingsLoadResult
{
    public required AppSettings Settings { get; init; }

    /// <summary>Set when a saved file existed but couldn't be used as-is.</summary>
    public string? Warning { get; init; }
}

public static class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static string DefaultPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KeyTapLoop", "settings.json");

    public static SettingsLoadResult Load(string path)
    {
        if (!File.Exists(path))
            return new SettingsLoadResult { Settings = new AppSettings() };

        try
        {
            var json = File.ReadAllText(path);
            var parsed = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            if (parsed is null) throw new JsonException("empty");
            var clean = parsed.Sanitized(out var changed);
            return new SettingsLoadResult
            {
                Settings = clean,
                Warning = changed ? "Some saved settings were invalid, so defaults were used for them." : null,
            };
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return new SettingsLoadResult
            {
                Settings = new AppSettings(),
                Warning = "Saved settings couldn’t be read, so defaults were loaded.",
            };
        }
    }

    /// <summary>Saves settings. Returns false if the file couldn't be written; the app keeps working.</summary>
    public static bool TrySave(string path, AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var temp = path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(settings, JsonOptions));
            File.Move(temp, path, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return false;
        }
    }
}
