using KeyTapLoop.Core;

namespace KeyTapLoop.Core.Tests;

/// <summary>Virtual time: waiting just moves the clock forward. Fully deterministic, no real sleeping.</summary>
internal sealed class FakeClock : IRunClock
{
    public long NowMs { get; set; }

    public Task DelayUntilAsync(long targetMs, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (targetMs > NowMs) NowMs = targetMs;
        return Task.CompletedTask;
    }
}

internal sealed record SentEvent(long TimeMs, bool Down, int VirtualKey);

/// <summary>Records every key event and lets a test script the outside world.</summary>
internal sealed class FakeHost : IRunHost
{
    private readonly FakeClock clock;
    public FakeHost(FakeClock clock) => this.clock = clock;

    public List<SentEvent> Events { get; } = new();
    public bool Alive { get; set; } = true;
    public bool Foreground { get; set; } = true;
    public HashSet<int> HeldByUser { get; } = new();

    /// <summary>Return false for key-downs of these keys.</summary>
    public HashSet<int> RejectDown { get; } = new();

    /// <summary>Return false for key-ups of these keys.</summary>
    public HashSet<int> RejectUp { get; } = new();

    public Action<SentEvent>? OnEvent { get; set; }

    public bool IsTargetAlive() => Alive;
    public bool IsTargetForeground() => Foreground;
    public bool IsKeyHeld(int virtualKey) => HeldByUser.Contains(virtualKey);

    public bool KeyDown(int virtualKey, bool extended)
    {
        if (RejectDown.Contains(virtualKey)) return false;
        Record(true, virtualKey);
        return true;
    }

    public bool KeyUp(int virtualKey, bool extended)
    {
        if (RejectUp.Contains(virtualKey)) return false;
        Record(false, virtualKey);
        return true;
    }

    private void Record(bool down, int vk)
    {
        var e = new SentEvent(clock.NowMs, down, vk);
        Events.Add(e);
        OnEvent?.Invoke(e);
    }

    public IReadOnlyList<SentEvent> Downs(int vk) => Events.Where(e => e.Down && e.VirtualKey == vk).ToList();

    /// <summary>Every key that went down must come back up.</summary>
    public bool IsBalanced() =>
        Events.GroupBy(e => e.VirtualKey).All(g => g.Count(e => e.Down) == g.Count(e => !e.Down));
}
