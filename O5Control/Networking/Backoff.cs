using System;

namespace O5Control.Networking;

internal sealed class Backoff
{
    private static readonly Random Jitter = new();

    private readonly TimeSpan _initial;
    private readonly TimeSpan _maximum;
    private int _attempt;

    public Backoff(TimeSpan initial, TimeSpan maximum)
    {
        _initial = initial;
        _maximum = maximum;
    }

    public TimeSpan Next()
    {
        var exponent = Math.Min(_attempt++, 10);
        var milliseconds = Math.Min(_maximum.TotalMilliseconds, _initial.TotalMilliseconds * Math.Pow(2, exponent));

        double factor;
        lock (Jitter)
            factor = 0.75 + Jitter.NextDouble() * 0.5;

        return TimeSpan.FromMilliseconds(milliseconds * factor);
    }

    public void Reset() => _attempt = 0;
}
