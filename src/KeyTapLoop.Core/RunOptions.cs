namespace KeyTapLoop.Core;

public enum RunMode { Repeat, Hold }

/// <summary>A validated description of one run.</summary>
/// <param name="Output">The key or shortcut to send.</param>
/// <param name="Mode">Repeat taps it; Hold keeps it down.</param>
/// <param name="PeriodMs">Start-to-start time between taps (Repeat only).</param>
/// <param name="KeyDownMs">How long each tap keeps the key down (Repeat only).</param>
/// <param name="MaxTaps">Stop after this many completed taps (Repeat only).</param>
/// <param name="DurationMs">Stop after this long, measured from the first key-down.</param>
/// <param name="StartDelayMs">Countdown before the run begins. Zero means start at once.</param>
/// <param name="ReleaseBeforeStart">Keys (usually the Start shortcut) that must be physically up before the first output.</param>
public sealed record RunOptions(
    KeyChord Output,
    RunMode Mode,
    int PeriodMs,
    int KeyDownMs,
    int? MaxTaps,
    int? DurationMs,
    int StartDelayMs,
    IReadOnlyList<int> ReleaseBeforeStart)
{
    public const int DefaultPeriodMs = 200;
    public const int DefaultKeyDownMs = 40;
    public const int DefaultMaxTaps = 20;
    public const int DefaultDurationMs = 10_000;
    public const int DefaultStartDelaySeconds = 3;
}

public enum EndReason
{
    Completed,
    StoppedByUser,
    FocusChanged,
    TargetClosed,
    InputFailed,
}

/// <summary>Which limit ended a completed run.</summary>
public enum LimitKind { None, Count, Duration }

public sealed record RunResult(
    EndReason Reason,
    LimitKind Limit,
    int Taps,
    long ElapsedMs,
    string Detail,
    bool CleanupFailed)
{
    public string Title => Reason switch
    {
        EndReason.Completed => "Completed",
        EndReason.StoppedByUser => "Stopped by you",
        EndReason.FocusChanged => "Stopped because focus changed",
        EndReason.TargetClosed => "Target closed",
        EndReason.InputFailed => "Couldn’t send input",
        _ => Reason.ToString(),
    };
}
