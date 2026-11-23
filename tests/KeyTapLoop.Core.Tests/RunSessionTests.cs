using KeyTapLoop.Core;
using Xunit;

namespace KeyTapLoop.Core.Tests;

public class RunSessionTests
{
    private const int F = 'F';
    private const int LCtrl = 0xA2, LAlt = 0xA4, LShift = 0xA0;

    private static RunOptions Repeat(
        int? taps = 5, int? durationMs = null, int period = 200, int keyDown = 40,
        KeyChord? output = null, int delayMs = 0, int[]? releaseFirst = null) =>
        new(output ?? new KeyChord(F, false, false, false), RunMode.Repeat, period, keyDown, taps, durationMs, delayMs,
            releaseFirst ?? Array.Empty<int>());

    private static RunOptions Hold(int durationMs = 1000, KeyChord? output = null) =>
        new(output ?? new KeyChord(F, false, false, false), RunMode.Hold, 0, 0, null, durationMs, 0, Array.Empty<int>());

    private static (RunSession session, FakeHost host, FakeClock clock) Make(RunOptions options)
    {
        var clock = new FakeClock();
        var host = new FakeHost(clock);
        return (new RunSession(options, host, clock), host, clock);
    }

    // ---- Limits ----------------------------------------------------------------------

    [Fact]
    public async Task Count_cap_sends_exactly_that_many_taps()
    {
        var (session, host, _) = Make(Repeat(taps: 5));
        var result = await session.RunAsync();

        Assert.Equal(EndReason.Completed, result.Reason);
        Assert.Equal(LimitKind.Count, result.Limit);
        Assert.Equal(5, result.Taps);
        Assert.Equal(5, host.Downs(F).Count);
        Assert.True(host.IsBalanced());
    }

    [Fact]
    public async Task Taps_start_one_period_apart_and_stay_down_for_the_key_down_time()
    {
        var (session, host, _) = Make(Repeat(taps: 3, period: 200, keyDown: 40));
        await session.RunAsync();

        Assert.Equal(new long[] { 0, 200, 400 }, host.Downs(F).Select(e => e.TimeMs));
        Assert.Equal(new long[] { 40, 240, 440 }, host.Events.Where(e => !e.Down).Select(e => e.TimeMs));
    }

    [Fact]
    public async Task Duration_cap_ends_the_run_and_stops_new_key_downs()
    {
        var (session, host, _) = Make(Repeat(taps: null, durationMs: 1000));
        var result = await session.RunAsync();

        Assert.Equal(EndReason.Completed, result.Reason);
        Assert.Equal(LimitKind.Duration, result.Limit);
        Assert.Equal(5, result.Taps);                        // slots at 0,200,400,600,800; 1000 is the deadline
        Assert.All(host.Downs(F), e => Assert.True(e.TimeMs < 1000));
        Assert.True(result.ElapsedMs >= 1000);
        Assert.True(host.IsBalanced());
    }

    [Fact]
    public async Task When_both_caps_are_set_the_count_can_win()
    {
        var (session, _, _) = Make(Repeat(taps: 3, durationMs: 60_000));
        var result = await session.RunAsync();
        Assert.Equal(LimitKind.Count, result.Limit);
        Assert.Equal(3, result.Taps);
    }

    [Fact]
    public async Task When_both_caps_are_set_the_duration_can_win()
    {
        var (session, _, _) = Make(Repeat(taps: 1000, durationMs: 500));
        var result = await session.RunAsync();
        Assert.Equal(LimitKind.Duration, result.Limit);
        Assert.Equal(3, result.Taps);                        // 0, 200, 400
    }

    [Fact]
    public async Task A_stall_skips_missed_slots_instead_of_bursting()
    {
        var clock = new FakeClock();
        var host = new FakeHost(clock);
        var stalled = false;
        host.OnEvent = e =>
        {
            // After the first tap's key-up, the machine freezes for 1 second.
            if (!e.Down && !stalled) { stalled = true; clock.NowMs += 1000; }
        };
        var session = new RunSession(Repeat(taps: null, durationMs: 2000), host, clock);
        var result = await session.RunAsync();

        var times = host.Downs(F).Select(e => e.TimeMs).ToList();
        Assert.Equal(0, times[0]);
        // 40 ms key-down + 1000 ms stall = 1040; the next slot after that is 1200, not 200/400/600/800/1000.
        Assert.Equal(1200, times[1]);
        for (var i = 2; i < times.Count; i++) Assert.Equal(200, times[i] - times[i - 1]);   // steady again after the stall
        Assert.Equal(times.Count, times.Distinct().Count());                                // never two taps in one slot
        Assert.Equal(EndReason.Completed, result.Reason);
    }

    // ---- Stopping --------------------------------------------------------------------

    [Fact]
    public async Task Stop_while_a_key_is_down_releases_it()
    {
        var (session, host, _) = Make(Repeat(taps: 100));
        host.OnEvent = e => { if (e.Down && host.Downs(F).Count == 3) session.Stop(); };

        var result = await session.RunAsync();

        Assert.Equal(EndReason.StoppedByUser, result.Reason);
        Assert.Equal(2, result.Taps);                        // the third tap was interrupted before it completed
        Assert.True(host.IsBalanced());
        Assert.False(result.CleanupFailed);
        Assert.False(host.Events.Last().Down);
    }

    [Fact]
    public async Task Stop_during_countdown_sends_nothing()
    {
        var (session, host, _) = Make(Repeat(delayMs: 3000));
        session.Stop();
        var result = await session.RunAsync();

        Assert.Equal(EndReason.StoppedByUser, result.Reason);
        Assert.Empty(host.Events);
        Assert.Equal(0, result.Taps);
    }

    [Fact]
    public async Task Stop_requested_twice_is_harmless()
    {
        var (session, host, _) = Make(Repeat(taps: 100));
        host.OnEvent = e => { if (e.Down) { session.Stop(); session.Stop(); } };
        var result = await session.RunAsync();
        Assert.Equal(EndReason.StoppedByUser, result.Reason);
        Assert.True(host.IsBalanced());
    }

    [Fact]
    public async Task Stop_after_completion_does_not_change_the_result()
    {
        var (session, _, _) = Make(Repeat(taps: 2));
        var result = await session.RunAsync();
        session.Stop();
        Assert.Equal(EndReason.Completed, result.Reason);
        Assert.Equal(RunPhase.Ended, session.Phase);
    }

    // ---- Focus and target ------------------------------------------------------------

    [Fact]
    public async Task Losing_foreground_ends_the_run_with_no_more_key_downs()
    {
        var (session, host, _) = Make(Repeat(taps: 100));
        host.OnEvent = e => { if (!e.Down && host.Downs(F).Count == 2) host.Foreground = false; };

        var result = await session.RunAsync();

        Assert.Equal(EndReason.FocusChanged, result.Reason);
        Assert.Equal(2, host.Downs(F).Count);
        Assert.Equal(2, result.Taps);
    }

    [Fact]
    public async Task A_quick_switch_away_and_back_still_ends_the_run()
    {
        var (session, host, _) = Make(Repeat(taps: 100));
        host.OnEvent = e =>
        {
            if (!e.Down && host.Downs(F).Count == 2)
            {
                // Windows reports the change even though focus is back by the next check.
                session.OnForegroundChanged(isTarget: false);
                session.OnForegroundChanged(isTarget: true);
            }
        };

        var result = await session.RunAsync();

        Assert.Equal(EndReason.FocusChanged, result.Reason);
        Assert.Equal(2, host.Downs(F).Count);
        Assert.True(host.Foreground);
    }

    [Fact]
    public async Task Focus_change_while_the_key_is_held_releases_it()
    {
        var (session, host, _) = Make(Hold(durationMs: 5000));
        host.OnEvent = e => { if (e.Down) session.OnForegroundChanged(isTarget: false); };

        var result = await session.RunAsync();

        Assert.Equal(EndReason.FocusChanged, result.Reason);
        Assert.True(host.IsBalanced());
        Assert.False(host.Events.Last().Down);
    }

    [Fact]
    public async Task Focus_events_during_the_countdown_are_ignored()
    {
        var clock = new FakeClock();
        var host = new FakeHost(clock);
        var session = new RunSession(Repeat(taps: 2, delayMs: 3000), host, clock);
        session.OnForegroundChanged(isTarget: false);        // user is switching to the target
        var result = await session.RunAsync();
        Assert.Equal(EndReason.Completed, result.Reason);
    }

    [Fact]
    public async Task Run_does_not_start_if_the_target_is_not_in_front_when_the_countdown_ends()
    {
        var (session, host, _) = Make(Repeat(delayMs: 1000));
        host.Foreground = false;
        var result = await session.RunAsync();
        Assert.Equal(EndReason.FocusChanged, result.Reason);
        Assert.Empty(host.Events);
    }

    [Fact]
    public async Task A_closed_target_ends_the_run()
    {
        var (session, host, _) = Make(Repeat(taps: 100));
        host.OnEvent = e => { if (!e.Down && host.Downs(F).Count == 2) { host.Alive = false; host.Foreground = false; } };

        var result = await session.RunAsync();

        Assert.Equal(EndReason.TargetClosed, result.Reason);
        Assert.Equal(2, host.Downs(F).Count);
    }

    [Fact]
    public async Task A_focus_event_for_a_dead_target_is_reported_as_closed()
    {
        var (session, host, _) = Make(Repeat(taps: 100));
        host.OnEvent = e =>
        {
            if (e.Down && host.Downs(F).Count == 2) { host.Alive = false; session.OnForegroundChanged(isTarget: false); }
        };
        var result = await session.RunAsync();
        Assert.Equal(EndReason.TargetClosed, result.Reason);
    }

    // ---- Chords and cleanup ------------------------------------------------------------

    [Fact]
    public async Task A_chord_presses_modifiers_first_and_releases_in_reverse()
    {
        var chord = new KeyChord(F, Ctrl: true, Alt: true, Shift: true);
        var (session, host, _) = Make(Repeat(taps: 1, output: chord));
        await session.RunAsync();

        var sequence = host.Events.Select(e => (e.Down ? "+" : "-") + e.VirtualKey).ToArray();
        Assert.Equal(new[] { $"+{LCtrl}", $"+{LAlt}", $"+{LShift}", $"+{F}", $"-{F}", $"-{LShift}", $"-{LAlt}", $"-{LCtrl}" }, sequence);
    }

    [Fact]
    public async Task A_rejected_key_down_releases_the_keys_already_pressed()
    {
        var chord = new KeyChord(F, Ctrl: true, Alt: false, Shift: true);
        var (session, host, _) = Make(Repeat(taps: 5, output: chord));
        host.RejectDown.Add(F);

        var result = await session.RunAsync();

        Assert.Equal(EndReason.InputFailed, result.Reason);
        Assert.Equal(0, result.Taps);
        Assert.True(host.IsBalanced());                      // Ctrl and Shift went down and came back up
        Assert.Equal(2, host.Events.Count(e => !e.Down));
        Assert.False(result.CleanupFailed);
    }

    [Fact]
    public async Task A_refused_release_is_reported_as_a_cleanup_failure()
    {
        var (session, host, _) = Make(Repeat(taps: 3));
        host.RejectUp.Add(F);

        var result = await session.RunAsync();

        Assert.Equal(EndReason.InputFailed, result.Reason);
        Assert.True(result.CleanupFailed);
        Assert.Contains("still be down", result.Detail);
    }

    [Fact]
    public async Task Keys_the_user_is_already_holding_are_never_adopted()
    {
        var chord = new KeyChord(F, Ctrl: true, Alt: false, Shift: false);
        var (session, host, _) = Make(Repeat(taps: 3, output: chord));
        host.HeldByUser.Add(KeyChord.VkControl);

        var result = await session.RunAsync();

        Assert.Equal(EndReason.InputFailed, result.Reason);
        Assert.Empty(host.Events);                           // nothing sent, so nothing released
    }

    // ---- Start shortcut handling ---------------------------------------------------------

    [Fact]
    public async Task First_output_waits_for_the_start_shortcut_to_be_released()
    {
        var clock = new FakeClock();
        var host = new FakeHost(clock);
        host.HeldByUser.Add(KeyChord.VkControl);
        host.HeldByUser.Add(0x70);
        var releasedAt = -1L;
        // The user lets go of the shortcut after 300 ms of waiting.
        var wrapped = new ReleasingHost(host, clock, 300, () => releasedAt = clock.NowMs);
        var session = new RunSession(Repeat(taps: 1, releaseFirst: new[] { KeyChord.VkControl, 0x70 }), wrapped, clock);
        var result = await session.RunAsync();

        Assert.Equal(EndReason.Completed, result.Reason);
        Assert.True(releasedAt >= 300);
        Assert.True(host.Downs(F)[0].TimeMs >= releasedAt);
    }

    [Fact]
    public async Task A_start_shortcut_that_stays_held_fails_clearly_and_sends_nothing()
    {
        var (session, host, _) = Make(Repeat(taps: 1, releaseFirst: new[] { 0x70 }));
        host.HeldByUser.Add(0x70);

        var result = await session.RunAsync();

        Assert.Equal(EndReason.InputFailed, result.Reason);
        Assert.Contains("Start shortcut", result.Detail);
        Assert.Empty(host.Events);
    }

    // ---- Running until stopped -------------------------------------------------------------

    [Fact]
    public async Task Repeat_with_no_limit_keeps_going_until_stopped()
    {
        var (session, host, _) = Make(Repeat(taps: null, durationMs: null));
        host.OnEvent = e => { if (!e.Down && host.Downs(F).Count == 250) session.Stop(); };

        var result = await session.RunAsync();

        Assert.Equal(EndReason.StoppedByUser, result.Reason);
        Assert.Equal(250, result.Taps);
        Assert.True(host.IsBalanced());
    }

    [Fact]
    public async Task Repeat_with_no_limit_still_ends_when_focus_moves()
    {
        var (session, host, _) = Make(Repeat(taps: null, durationMs: null));
        host.OnEvent = e => { if (!e.Down && host.Downs(F).Count == 10) host.Foreground = false; };

        var result = await session.RunAsync();

        Assert.Equal(EndReason.FocusChanged, result.Reason);
        Assert.Equal(10, host.Downs(F).Count);
    }

    [Fact]
    public async Task Hold_with_no_time_limit_holds_until_stopped_then_releases()
    {
        var fake = new FakeClock();
        var host = new FakeHost(fake);
        var clock = new StopAfterClock(fake, virtualHours: 5);
        var options = new RunOptions(new KeyChord(F, false, false, false), RunMode.Hold, 0, 0, null, null, 0, Array.Empty<int>());
        var session = new RunSession(options, host, clock);
        clock.Session = session;

        var result = await session.RunAsync();

        Assert.Equal(EndReason.StoppedByUser, result.Reason);
        Assert.True(result.ElapsedMs >= 5 * 3_600_000L);     // it really did keep holding
        Assert.Single(host.Downs(F));
        Assert.True(host.IsBalanced());
        Assert.False(host.Events.Last().Down);
    }

    /// <summary>A virtual clock that presses Stop once some virtual hours have gone by.</summary>
    private sealed class StopAfterClock : IRunClock
    {
        private readonly FakeClock inner;
        private readonly long limitMs;
        public RunSession? Session { get; set; }

        public StopAfterClock(FakeClock inner, int virtualHours)
        {
            this.inner = inner;
            limitMs = virtualHours * 3_600_000L;
        }

        public long NowMs => inner.NowMs;

        public Task DelayUntilAsync(long targetMs, CancellationToken token)
        {
            if (inner.NowMs >= limitMs) Session!.Stop();
            return inner.DelayUntilAsync(targetMs, token);   // throws once Stop has cancelled the token
        }
    }

    // ---- Hold ----------------------------------------------------------------------------

    [Fact]
    public async Task Hold_releases_at_the_time_limit()
    {
        var (session, host, _) = Make(Hold(durationMs: 1500));
        var result = await session.RunAsync();

        Assert.Equal(EndReason.Completed, result.Reason);
        Assert.Equal(LimitKind.Duration, result.Limit);
        Assert.Single(host.Downs(F));
        Assert.Equal(1500, host.Events.Single(e => !e.Down).TimeMs);
    }

    [Fact]
    public async Task Stopping_a_held_chord_releases_every_key()
    {
        var chord = new KeyChord(F, Ctrl: true, Alt: false, Shift: false);
        var (session, host, _) = Make(Hold(durationMs: 60_000, output: chord));
        host.OnEvent = e => { if (e.Down && e.VirtualKey == F) session.Stop(); };

        var result = await session.RunAsync();

        Assert.Equal(EndReason.StoppedByUser, result.Reason);
        Assert.True(host.IsBalanced());
        Assert.Equal(LCtrl, host.Events.Last().VirtualKey);
    }

    // ---- Progress reporting ------------------------------------------------------------------

    [Fact]
    public async Task Countdown_is_reported_and_elapsed_starts_at_the_first_key()
    {
        var (session, host, clock) = Make(Repeat(taps: 2, delayMs: 3000));
        var result = await session.RunAsync();

        Assert.Equal(3000, host.Downs(F)[0].TimeMs);
        Assert.Equal(240, result.ElapsedMs);                 // second tap starts at +200, releases at +240
        Assert.Equal(3240, clock.NowMs);
    }

    /// <summary>Wraps a host so the user's held keys are released after a fixed amount of polling time.</summary>
    private sealed class ReleasingHost : IRunHost
    {
        private readonly FakeHost inner;
        private readonly FakeClock clock;
        private readonly long releaseAfterMs;
        private readonly Action onRelease;
        private bool released;

        public ReleasingHost(FakeHost inner, FakeClock clock, long releaseAfterMs, Action onRelease)
        {
            this.inner = inner; this.clock = clock; this.releaseAfterMs = releaseAfterMs; this.onRelease = onRelease;
        }

        public bool IsTargetAlive() => inner.IsTargetAlive();
        public bool IsTargetForeground() => inner.IsTargetForeground();
        public bool KeyDown(int vk, bool ext) => inner.KeyDown(vk, ext);
        public bool KeyUp(int vk, bool ext) => inner.KeyUp(vk, ext);

        public bool IsKeyHeld(int vk)
        {
            if (!released && clock.NowMs >= releaseAfterMs)
            {
                released = true;
                inner.HeldByUser.Clear();
                onRelease();
            }
            return inner.IsKeyHeld(vk);
        }
    }
}
