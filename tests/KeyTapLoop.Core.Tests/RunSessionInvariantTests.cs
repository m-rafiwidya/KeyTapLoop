using KeyTapLoop.Core;
using Xunit;

namespace KeyTapLoop.Core.Tests;

/// <summary>
/// Thousands of randomised runs with stops, focus losses, closed targets, refused key-ups and machine stalls
/// thrown in at random moments. Whatever happens, a run must never leave a key down, send a key after it was
/// told to end, or exceed its limits. Seeds are fixed, so any failure is reproducible.
/// </summary>
public class RunSessionInvariantTests
{
    private const int F = 'F';

    private enum Trigger { None, Stop, FocusLost, FocusEvent, TargetClosed }

    [Fact]
    public async Task Random_runs_never_break_the_safety_invariants()
    {
        for (var seed = 0; seed < 3000; seed++)
        {
            try { await RunScenario(seed); }
            catch (Exception ex) when (ex is not Xunit.Sdk.XunitException)
            {
                throw new Xunit.Sdk.XunitException($"Seed {seed} threw {ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    private static async Task RunScenario(int seed)
    {
        var rng = new Random(seed);

        var hold = rng.Next(5) == 0;
        var period = rng.Next(10, 500);
        var keyDown = rng.Next(1, period);
        int? maxTaps = hold || rng.Next(3) == 0 ? null : rng.Next(1, 30);
        int? duration = rng.Next(3) == 0 ? null : rng.Next(50, 5000);
        var chord = rng.Next(3) switch
        {
            0 => new KeyChord(F, false, false, false),
            1 => new KeyChord(F, true, false, false),
            _ => new KeyChord(F, true, true, true),
        };
        var delay = rng.Next(4) == 0 ? rng.Next(100, 3000) : 0;
        var options = new RunOptions(chord, hold ? RunMode.Hold : RunMode.Repeat, hold ? 0 : period, hold ? 0 : keyDown,
            maxTaps, duration, delay, Array.Empty<int>());

        var unbounded = hold ? duration is null : maxTaps is null && duration is null;
        var trigger = unbounded || rng.Next(2) == 0 ? (Trigger)rng.Next(1, 5) : Trigger.None;
        var triggerAfterEvents = hold ? 1 : rng.Next(1, 40);   // a hold only produces a few events
        var refuseUps = rng.Next(25) == 0;
        var stallEvery = rng.Next(4) == 0 ? rng.Next(3, 9) : 0;
        var stallMs = rng.Next(period, period * 4);

        var clock = new FakeClock();
        var host = new FakeHost(clock);
        var session = new RunSession(options, host, clock);
        if (refuseUps) host.RejectUp.Add(F);

        var eventCount = 0;
        var triggeredAtEvent = -1;
        var stalled = false;
        host.OnEvent = e =>
        {
            eventCount++;
            if (stallEvery > 0 && !e.Down && eventCount % stallEvery == 0) { clock.NowMs += stallMs; stalled = true; }

            // Only fire on a main-key down or any key-up, so no further chord keys are mid-flight.
            var safeMoment = !e.Down || e.VirtualKey == F;
            if (trigger == Trigger.None || triggeredAtEvent >= 0 || eventCount < triggerAfterEvents || !safeMoment) return;
            triggeredAtEvent = host.Events.Count;
            switch (trigger)
            {
                case Trigger.Stop: session.Stop(); break;
                case Trigger.FocusLost: host.Foreground = false; break;
                case Trigger.FocusEvent: session.OnForegroundChanged(isTarget: false); break;
                case Trigger.TargetClosed: host.Alive = false; host.Foreground = false; break;
            }
        };

        var result = await session.RunAsync();
        var ctx = $"seed {seed}, {options}, trigger {trigger}@{triggerAfterEvents}, refuseUps {refuseUps}";

        // 1. Nothing is left pressed, unless the host itself refused to let go (and the result then says so).
        if (!refuseUps) Assert.True(host.IsBalanced(), "stuck key; " + ctx);
        else if (host.Events.Any(e => e.Down)) Assert.True(result.CleanupFailed || host.IsBalanced(), "unreported stuck key; " + ctx);

        // 2. After the trigger moment, no new key goes down.
        if (triggeredAtEvent >= 0)
            Assert.DoesNotContain(host.Events.Skip(triggeredAtEvent), e => e.Down);

        // 3. Never exceed limits.
        var downs = host.Downs(F);
        if (maxTaps is { } cap) Assert.True(result.Taps <= cap, "too many taps; " + ctx);
        Assert.True(downs.Count == 0 || downs.Count >= result.Taps, "tap count exceeds key-downs; " + ctx);
        if (hold) Assert.True(downs.Count <= 1, "hold pressed more than once; " + ctx);

        // 4. A taps counted only if released, so at most one key-down is unaccounted for.
        if (!hold && !refuseUps) Assert.True(downs.Count - result.Taps is 0 or 1, "tap bookkeeping off; " + ctx);

        // 5. No key goes down at or after the time limit.
        if (duration is { } d && downs.Count > 0)
        {
            var start = downs[0].TimeMs; // the first key-down marks the start of the run
            Assert.All(downs, e => Assert.True(e.TimeMs < start + d, "key-down after the time limit; " + ctx));
        }

        // 6. Taps land on period boundaries unless the machine stalled.
        if (!hold && !stalled && downs.Count > 1)
            Assert.All(downs, e => Assert.Equal(0, (e.TimeMs - downs[0].TimeMs) % period));

        // 7. The reason matches what happened.
        if (trigger == Trigger.None && !refuseUps) Assert.Equal(EndReason.Completed, result.Reason);
        if (triggeredAtEvent >= 0 && !refuseUps && result.Reason != EndReason.Completed)
        {
            var expected = trigger switch
            {
                Trigger.Stop => EndReason.StoppedByUser,
                Trigger.TargetClosed => EndReason.TargetClosed,
                _ => EndReason.FocusChanged,
            };
            Assert.Equal(expected, result.Reason);
        }
        if (result.Reason == EndReason.Completed) Assert.NotEqual(LimitKind.None, result.Limit);
        Assert.Equal(RunPhase.Ended, session.Phase);
    }
}
