namespace ReplyInMyVoice.Infrastructure.Services;

/// <summary>
/// Per-process current-minute arrival gate. It counts only traffic seen by this
/// instance; the database limiter remains the source of truth across instances.
/// </summary>
public sealed class InProcessRateLimitGate
{
    private const int SweepThreshold = 10_000;

    private readonly object gate = new();
    private readonly Dictionary<Guid, WindowCounter> counters = [];

    public bool ShouldShed(
        Guid key,
        int limitPerMinute,
        DateTimeOffset now,
        out DateTimeOffset resetAt)
    {
        var windowStart = ToMinuteWindowStart(now);
        resetAt = windowStart.AddMinutes(1);
        if (limitPerMinute <= 0)
        {
            return false;
        }

        lock (gate)
        {
            if (counters.Count > SweepThreshold)
            {
                SweepStaleEntries(windowStart);
            }

            if (!counters.TryGetValue(key, out var counter) || counter.WindowStart != windowStart)
            {
                counter = new WindowCounter(windowStart, 0);
            }

            counter.Count += 1;
            counters[key] = counter;
            return counter.Count > limitPerMinute;
        }
    }

    private void SweepStaleEntries(DateTimeOffset currentWindowStart)
    {
        foreach (var key in counters
            .Where(x => x.Value.WindowStart != currentWindowStart)
            .Select(x => x.Key)
            .ToList())
        {
            counters.Remove(key);
        }
    }

    private static DateTimeOffset ToMinuteWindowStart(DateTimeOffset now)
    {
        var utc = now.ToUniversalTime();
        return new DateTimeOffset(
            utc.Year,
            utc.Month,
            utc.Day,
            utc.Hour,
            utc.Minute,
            0,
            TimeSpan.Zero);
    }

    private sealed class WindowCounter(DateTimeOffset windowStart, int count)
    {
        public DateTimeOffset WindowStart { get; } = windowStart;

        public int Count { get; set; } = count;
    }
}
