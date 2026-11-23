using System.Globalization;

namespace KeyTapLoop.Core;

public enum Field { Output, Period, KeyDown, Count, Duration, Delay }

/// <summary>Raw values as typed on screen. Text fields stay strings so errors can point at them.</summary>
public sealed class RunInput
{
    public KeyChord? Output { get; set; }
    public RunMode Mode { get; set; } = RunMode.Repeat;
    public string Period { get; set; } = "";
    public string KeyDown { get; set; } = "";
    public bool CountEnabled { get; set; }
    public string Count { get; set; } = "";
    public bool DurationEnabled { get; set; }
    public string DurationSeconds { get; set; } = "";
    public string DelaySeconds { get; set; } = "";
    public KeyChord StartHotkey { get; set; }
    public KeyChord StopHotkey { get; set; }
    public KeyChord ToggleHotkey { get; set; }
}

public sealed record ValidationResult(RunOptions? Options, IReadOnlyDictionary<Field, string> Errors)
{
    public bool IsValid => Options is not null && Errors.Count == 0;
}

public static class RunValidator
{
    public const int MinPeriodMs = 10;
    public const int MaxPeriodMs = 3_600_000;
    public const int MaxKeyDownMs = 3_600_000;
    public const int MaxTaps = 1_000_000;
    public const double MinDurationSeconds = 0.1;
    public const double MaxDurationSeconds = 86_400;
    public const int MaxDelaySeconds = 60;

    public static ValidationResult Validate(RunInput input)
    {
        var errors = new Dictionary<Field, string>();
        var hold = input.Mode == RunMode.Hold;

        // Output
        if (input.Output is not { } output)
        {
            errors[Field.Output] = "Choose a key to send.";
            output = default;
        }
        else if (ReservedChords.IsReserved(output, out var reserved))
        {
            errors[Field.Output] = reserved + " Pick another key.";
        }
        else if (output == input.StartHotkey || output == input.StopHotkey || output == input.ToggleHotkey)
        {
            errors[Field.Output] = $"{output} is one of KeyTapLoop’s own shortcuts. Pick another key or change the shortcut.";
        }

        // Timing (Repeat only)
        var period = 0;
        var keyDown = 0;
        if (!hold)
        {
            var periodOk = TryWhole(input.Period, MinPeriodMs, MaxPeriodMs, "Period", "ms", out period, out var periodError);
            if (!periodOk) errors[Field.Period] = periodError;

            var keyDownOk = TryWhole(input.KeyDown, 1, MaxKeyDownMs, "Key-down time", "ms", out keyDown, out var keyDownError);
            if (!keyDownOk) errors[Field.KeyDown] = keyDownError;

            if (periodOk && keyDownOk && keyDown >= period)
                errors[Field.Period] = $"Period must be longer than the key-down time ({keyDown} ms).";
        }

        // Caps
        int? maxTaps = null;
        int? durationMs = null;
        if (!hold && input.CountEnabled)
        {
            if (TryWhole(input.Count, 1, MaxTaps, "Tap count", "taps", out var count, out var countError)) maxTaps = count;
            else errors[Field.Count] = countError;
        }

        // Limits are optional. With none set, the run lasts until the user stops it.
        if (input.DurationEnabled)
        {
            if (TryDecimal(input.DurationSeconds, MinDurationSeconds, MaxDurationSeconds, out var seconds, out var durationError))
                durationMs = (int)Math.Round(seconds * 1000);
            else errors[Field.Duration] = durationError;
        }

        // Start delay
        var delayMs = 0;
        if (TryWhole(input.DelaySeconds, 0, MaxDelaySeconds, "Start delay", "seconds", out var delay, out var delayError)) delayMs = delay * 1000;
        else errors[Field.Delay] = delayError;

        if (errors.Count > 0) return new ValidationResult(null, errors);

        var options = new RunOptions(output, input.Mode, period, keyDown, maxTaps, durationMs, delayMs, Array.Empty<int>());
        return new ValidationResult(options, errors);
    }

    /// <summary>Checks the Start, Stop and Toggle shortcuts. Returns null when all three are acceptable.</summary>
    public static string? ValidateHotkeys(KeyChord start, KeyChord stop, KeyChord toggle)
    {
        foreach (var (name, chord) in new[] { ("Start", start), ("Stop", stop), ("Toggle", toggle) })
        {
            if (!chord.HasModifier) return $"{name} needs Ctrl, Alt or Shift so it doesn’t swallow normal typing.";
            if (chord.VirtualKey is KeyCatalog.VkTab or KeyCatalog.VkEscape) return $"{name} can’t use Tab or Escape.";
            if (ReservedChords.IsReserved(chord, out var reason)) return $"{name}: {reason}";
        }
        if (start == stop || start == toggle || stop == toggle) return "Start, Stop and Toggle each need a different shortcut.";
        return null;
    }

    private static bool TryWhole(string text, int min, int max, string label, string unit, out int value, out string error)
    {
        value = 0;
        var trimmed = (text ?? "").Trim();
        if (trimmed.Length == 0) { error = $"{label} is required ({unit})."; return false; }
        if (!int.TryParse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture, out value))
        {
            error = $"{label} must be a whole number of {unit}.";
            return false;
        }
        if (value < min || value > max)
        {
            error = $"{label} must be between {min:N0} and {max:N0} {unit}.";
            return false;
        }
        error = "";
        return true;
    }

    private static bool TryDecimal(string text, double min, double max, out double value, out string error)
    {
        value = 0;
        var trimmed = (text ?? "").Trim().Replace(',', '.');
        if (trimmed.Length == 0) { error = "Time limit is required (seconds)."; return false; }
        if (!double.TryParse(trimmed, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value) || double.IsNaN(value))
        {
            error = "Time limit must be a number of seconds, like 10 or 2.5.";
            return false;
        }
        if (value < min || value > max)
        {
            error = $"Time limit must be between {min} and {max:N0} seconds.";
            return false;
        }
        error = "";
        return true;
    }
}
