using System.Media;

namespace KeyTapLoop.Native;

internal enum Cue
{
    /// <summary>Rising two-tone: keys are about to go out.</summary>
    Start,

    /// <summary>Falling two-tone: the run finished or you stopped it.</summary>
    Stop,

    /// <summary>Low double beep: something other than you ended the run (focus moved, target closed, input failed).</summary>
    Alert,
}

/// <summary>
/// Short tones for run start and end, so you can tell what happened without looking at the window.
/// The tones are generated in memory, so there are no audio files, and they play on the default output device.
/// </summary>
internal sealed class SoundCues : IDisposable
{
    private const int SampleRate = 44100;
    private const double Volume = 0.30;

    private readonly Dictionary<Cue, SoundPlayer> players = new();

    public bool Enabled { get; set; } = true;

    public SoundCues()
    {
        // Sound is a nicety. If a tone can't be prepared (no audio support), the app just stays silent.
        TryAdd(Cue.Start, (660, 70), (880, 110));
        TryAdd(Cue.Stop, (880, 70), (550, 120));
        TryAdd(Cue.Alert, (330, 110), (0, 50), (330, 110));
    }

    private void TryAdd(Cue cue, params (int Hz, int Ms)[] segments)
    {
        try { players[cue] = Build(segments); }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException or TimeoutException or PlatformNotSupportedException) { }
    }

    /// <summary>Plays a cue without blocking. A cue that can't play (no audio device) is silently skipped.</summary>
    public void Play(Cue cue)
    {
        if (!Enabled || !players.TryGetValue(cue, out var player)) return;
        try { player.Play(); }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException or TimeoutException) { }
    }

    public void Dispose()
    {
        foreach (var p in players.Values) p.Dispose();
        players.Clear();
    }

    /// <summary>One WAV made of (frequency Hz, length ms) segments. A frequency of 0 is silence.</summary>
    private static SoundPlayer Build(params (int Hz, int Ms)[] segments)
    {
        var samples = new List<short>();
        foreach (var (hz, ms) in segments)
        {
            var count = SampleRate * ms / 1000;
            var fade = Math.Min(count / 4, SampleRate / 200); // about 5 ms in and out, so there is no click
            for (var i = 0; i < count; i++)
            {
                if (hz == 0) { samples.Add(0); continue; }
                var envelope = i < fade ? (double)i / fade : i > count - fade ? (double)(count - i) / fade : 1.0;
                var value = Math.Sin(2 * Math.PI * hz * i / SampleRate) * Volume * envelope;
                samples.Add((short)(value * short.MaxValue));
            }
        }

        var stream = new MemoryStream();
        using (var w = new BinaryWriter(stream, System.Text.Encoding.ASCII, leaveOpen: true))
        {
            var dataBytes = samples.Count * 2;
            w.Write("RIFF"u8);
            w.Write(36 + dataBytes);
            w.Write("WAVEfmt "u8);
            w.Write(16);
            w.Write((short)1);           // PCM
            w.Write((short)1);           // mono
            w.Write(SampleRate);
            w.Write(SampleRate * 2);     // bytes per second
            w.Write((short)2);           // block align
            w.Write((short)16);          // bits per sample
            w.Write("data"u8);
            w.Write(dataBytes);
            foreach (var s in samples) w.Write(s);
        }
        stream.Position = 0;
        var player = new SoundPlayer(stream);
        player.Load();
        return player;
    }
}
