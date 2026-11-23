namespace KeyTapLoop.Core;

/// <summary>Monotonic time and cancellable waiting. Tests replace this with a virtual clock.</summary>
public interface IRunClock
{
    long NowMs { get; }

    /// <summary>Waits until <paramref name="targetMs"/>. Returns at once if it has already passed.
    /// Throws <see cref="OperationCanceledException"/> if the token is cancelled.</summary>
    Task DelayUntilAsync(long targetMs, CancellationToken token);
}

/// <summary>Everything a run needs from the outside world.</summary>
public interface IRunHost
{
    bool IsTargetAlive();
    bool IsTargetForeground();

    /// <summary>Sends one key-down. Returns true only if Windows accepted the event.</summary>
    bool KeyDown(int virtualKey, bool extended);

    /// <summary>Sends one key-up. Returns true only if Windows accepted the event.</summary>
    bool KeyUp(int virtualKey, bool extended);

    /// <summary>True if the user is physically holding this key right now.</summary>
    bool IsKeyHeld(int virtualKey);
}

public enum RunPhase { Idle, Countdown, Starting, Running, Ended }

/// <summary>
/// One countdown-then-run session. Create it, call <see cref="RunAsync"/> once, and use
/// <see cref="Stop"/> or <see cref="OnForegroundChanged"/> from any thread to end it.
/// A session never sends keys after it has ended and always tries to release keys it pressed.
/// </summary>
public sealed class RunSession
{
    public const int StartKeyReleaseTimeoutMs = 1500;
    private const int PollMs = 10;

    private readonly RunOptions options;
    private readonly IRunHost host;
    private readonly IRunClock clock;
    private readonly CancellationTokenSource cts = new();
    private readonly List<KeyStep> owned = new();

    private int endRequested;
    private EndReason requestedReason;
    private string requestedDetail = "";
    private volatile bool armed;
    private bool cleanupFailed;
    private int phase;
    private int taps;
    private long countdownEndMs;
    private long startMs = -1;
    private long endedElapsedMs = -1;

    public RunSession(RunOptions options, IRunHost host, IRunClock clock)
    {
        this.options = options;
        this.host = host;
        this.clock = clock;
    }

    public RunPhase Phase => (RunPhase)Volatile.Read(ref phase);

    /// <summary>Complete tap cycles Windows accepted (key-down and key-up).</summary>
    public int Taps => Volatile.Read(ref taps);

    public long ElapsedMs
    {
        get
        {
            var ended = Interlocked.Read(ref endedElapsedMs);
            if (ended >= 0) return ended;
            var start = Interlocked.Read(ref startMs);
            return start < 0 ? 0 : Math.Max(0, clock.NowMs - start);
        }
    }

    public long CountdownRemainingMs =>
        Phase == RunPhase.Countdown ? Math.Max(0, Interlocked.Read(ref countdownEndMs) - clock.NowMs) : 0;

    /// <summary>Ends the run as "stopped by you". Safe to call from any thread, any number of times.</summary>
    public void Stop() => RequestEnd(EndReason.StoppedByUser, "");

    /// <summary>
    /// Call whenever Windows reports the foreground window changed. Any change away from the target
    /// ends an armed run, even if focus comes straight back.
    /// </summary>
    public void OnForegroundChanged(bool isTarget)
    {
        if (!armed || isTarget) return;
        if (!host.IsTargetAlive()) RequestEnd(EndReason.TargetClosed, "The chosen window was closed.");
        else RequestEnd(EndReason.FocusChanged, "Another window took focus, so no more keys were sent. Start again to resume.");
    }

    private void RequestEnd(EndReason reason, string detail)
    {
        if (Interlocked.CompareExchange(ref endRequested, 1, 0) != 0) return;
        requestedReason = reason;
        requestedDetail = detail;
        try { cts.Cancel(); } catch (ObjectDisposedException) { }
    }

    private sealed record Outcome(EndReason Reason, LimitKind Limit, string Detail);

    public async Task<RunResult> RunAsync()
    {
        Outcome outcome;
        try
        {
            outcome = await RunCoreAsync(cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            outcome = RequestedOutcome();
        }
        catch (Exception ex)
        {
            outcome = new Outcome(EndReason.InputFailed, LimitKind.None, ex.Message);
        }

        armed = false;
        ReleaseOwned();
        var released = !cleanupFailed;
        var detail = outcome.Detail;
        if (!released)
            detail = (detail + " Windows refused to release a key, so one may still be down. Press and release it to clear it.").Trim();

        var startedAt = Interlocked.Read(ref startMs);
        var elapsed = startedAt < 0 ? 0 : Math.Max(0, clock.NowMs - startedAt);
        Interlocked.Exchange(ref endedElapsedMs, elapsed);
        Volatile.Write(ref phase, (int)RunPhase.Ended);
        return new RunResult(outcome.Reason, outcome.Limit, Taps, elapsed, detail, !released);
    }

    private Outcome RequestedOutcome() =>
        Volatile.Read(ref endRequested) == 1
            ? new Outcome(requestedReason, LimitKind.None, requestedDetail)
            : new Outcome(EndReason.StoppedByUser, LimitKind.None, "");

    private async Task<Outcome> RunCoreAsync(CancellationToken token)
    {
        if (options.StartDelayMs > 0)
        {
            Volatile.Write(ref phase, (int)RunPhase.Countdown);
            Interlocked.Exchange(ref countdownEndMs, clock.NowMs + options.StartDelayMs);
            await clock.DelayUntilAsync(Interlocked.Read(ref countdownEndMs), token).ConfigureAwait(false);
        }

        Volatile.Write(ref phase, (int)RunPhase.Starting);

        // Arm first, then check: a focus change in the gap cannot slip through.
        armed = true;
        token.ThrowIfCancellationRequested();
        if (!host.IsTargetAlive())
            return new Outcome(EndReason.TargetClosed, LimitKind.None, "The chosen window is gone. Choose a target again.");
        if (!host.IsTargetForeground())
            return new Outcome(EndReason.FocusChanged, LimitKind.None, "The chosen window wasn’t in the foreground when the run began.");

        if (options.ReleaseBeforeStart.Count > 0)
        {
            var deadline = clock.NowMs + StartKeyReleaseTimeoutMs;
            while (options.ReleaseBeforeStart.Any(host.IsKeyHeld))
            {
                if (clock.NowMs >= deadline)
                    return new Outcome(EndReason.InputFailed, LimitKind.None,
                        "The Start shortcut was still held down, so nothing was sent. Release it and try again.");
                await clock.DelayUntilAsync(clock.NowMs + PollMs, token).ConfigureAwait(false);
            }
        }

        token.ThrowIfCancellationRequested();

        // Never adopt a key the user is already holding.
        foreach (var vk in options.Output.WatchedKeys())
        {
            if (host.IsKeyHeld(vk))
                return new Outcome(EndReason.InputFailed, LimitKind.None,
                    "A key in this shortcut is already held down. Release it and start again.");
        }

        Interlocked.Exchange(ref startMs, clock.NowMs);
        Volatile.Write(ref phase, (int)RunPhase.Running);

        return options.Mode == RunMode.Hold
            ? await HoldAsync(token).ConfigureAwait(false)
            : await RepeatAsync(token).ConfigureAwait(false);
    }

    private async Task<Outcome> RepeatAsync(CancellationToken token)
    {
        var start = Interlocked.Read(ref startMs);
        var period = (long)options.PeriodMs;
        var deadline = options.DurationMs is { } d ? start + d : long.MaxValue;
        long slotIndex = 0;

        while (true)
        {
            var slot = start + slotIndex * period;
            if (slot >= deadline)
            {
                await clock.DelayUntilAsync(deadline, token).ConfigureAwait(false);
                return new Outcome(EndReason.Completed, LimitKind.Duration, "Reached the time limit.");
            }

            await clock.DelayUntilAsync(slot, token).ConfigureAwait(false);
            if (clock.NowMs >= deadline)
                return new Outcome(EndReason.Completed, LimitKind.Duration, "Reached the time limit.");

            var gate = CheckBeforeKeyDown();
            if (gate is not null) return gate;

            if (!PressChord(out var pressError))
                return new Outcome(EndReason.InputFailed, LimitKind.None, pressError);

            await clock.DelayUntilAsync(clock.NowMs + options.KeyDownMs, token).ConfigureAwait(false);

            if (!ReleaseOwned())
                return new Outcome(EndReason.InputFailed, LimitKind.None, "Windows didn’t accept a key release.");

            Interlocked.Increment(ref taps);
            if (options.MaxTaps is { } max && Taps >= max)
                return new Outcome(EndReason.Completed, LimitKind.Count, $"Reached {max:N0} taps.");

            // Skip slots we already missed instead of firing a burst to catch up.
            var now = clock.NowMs;
            slotIndex++;
            if (start + slotIndex * period < now)
                slotIndex = (now - start) / period + 1;
        }
    }

    private async Task<Outcome> HoldAsync(CancellationToken token)
    {
        var start = Interlocked.Read(ref startMs);

        var gate = CheckBeforeKeyDown();
        if (gate is not null) return gate;
        if (!PressChord(out var pressError))
            return new Outcome(EndReason.InputFailed, LimitKind.None, pressError);

        if (options.DurationMs is { } duration)
        {
            await clock.DelayUntilAsync(start + duration, token).ConfigureAwait(false);
        }
        else
        {
            // No time limit: keep holding until Stop, a focus change or the target closing cancels the wait.
            while (true) await clock.DelayUntilAsync(clock.NowMs + 3_600_000, token).ConfigureAwait(false);
        }

        if (!ReleaseOwned())
            return new Outcome(EndReason.InputFailed, LimitKind.None, "Windows didn’t accept a key release.");
        return new Outcome(EndReason.Completed, LimitKind.Duration, "Released the key at the time limit.");
    }

    /// <summary>Runs immediately before every new key-down. Returns an outcome if the run must end.</summary>
    private Outcome? CheckBeforeKeyDown()
    {
        if (!host.IsTargetAlive())
            return new Outcome(EndReason.TargetClosed, LimitKind.None, "The chosen window was closed.");
        if (!host.IsTargetForeground())
            return new Outcome(EndReason.FocusChanged, LimitKind.None,
                "The chosen window lost focus, so no more keys were sent. Start again to resume.");
        return null;
    }

    private bool PressChord(out string error)
    {
        foreach (var step in options.Output.PressOrder())
        {
            if (!host.KeyDown(step.VirtualKey, step.Extended))
            {
                error = $"Windows didn’t accept the {step.Label} key press.";
                return false;
            }
            owned.Add(step);
        }
        error = "";
        return true;
    }

    /// <summary>Releases keys this session pressed, last pressed first. Returns false if any release was refused.</summary>
    private bool ReleaseOwned()
    {
        var ok = true;
        for (var i = owned.Count - 1; i >= 0; i--)
        {
            var step = owned[i];
            if (!host.KeyUp(step.VirtualKey, step.Extended) && !host.KeyUp(step.VirtualKey, step.Extended))
                ok = false;
        }
        owned.Clear();
        if (!ok) cleanupFailed = true;
        return ok;
    }
}
