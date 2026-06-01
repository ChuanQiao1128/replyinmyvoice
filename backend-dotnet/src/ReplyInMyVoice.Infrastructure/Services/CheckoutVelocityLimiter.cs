namespace ReplyInMyVoice.Infrastructure.Services;

public interface ICheckoutVelocityLimiter
{
    CheckoutVelocityLimitResult Check(string identity);
}

public sealed class CheckoutVelocityLimiter : ICheckoutVelocityLimiter
{
    public const int DefaultMaxSessions = 5;
    public static readonly TimeSpan DefaultWindow = TimeSpan.FromMinutes(10);

    private readonly object gate = new();
    private readonly TimeProvider timeProvider;
    private readonly int maxSessions;
    private readonly TimeSpan window;
    private readonly Dictionary<string, Queue<DateTimeOffset>> buckets = new(StringComparer.Ordinal);

    public CheckoutVelocityLimiter()
        : this(TimeProvider.System, DefaultMaxSessions, DefaultWindow)
    {
    }

    public CheckoutVelocityLimiter(TimeProvider timeProvider, int maxSessions, TimeSpan window)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        if (maxSessions <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxSessions), "Checkout velocity limit must be positive.");
        }

        if (window <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(window), "Checkout velocity window must be positive.");
        }

        this.timeProvider = timeProvider;
        this.maxSessions = maxSessions;
        this.window = window;
    }

    public CheckoutVelocityLimitResult Check(string identity)
    {
        var key = NormalizeIdentity(identity);
        var now = timeProvider.GetUtcNow();

        lock (gate)
        {
            var bucket = GetBucket(key);
            Prune(bucket, now);

            if (bucket.Count >= maxSessions)
            {
                var retryAt = bucket.Peek().Add(window);
                return CheckoutVelocityLimitResult.Rejected(
                    retryAt,
                    retryAt - now,
                    maxSessions,
                    window);
            }

            bucket.Enqueue(now);
            return CheckoutVelocityLimitResult.Accepted(maxSessions, window);
        }
    }

    private Queue<DateTimeOffset> GetBucket(string key)
    {
        if (!buckets.TryGetValue(key, out var bucket))
        {
            bucket = new Queue<DateTimeOffset>();
            buckets[key] = bucket;
        }

        return bucket;
    }

    private void Prune(Queue<DateTimeOffset> bucket, DateTimeOffset now)
    {
        while (bucket.TryPeek(out var oldest) && now - oldest >= window)
        {
            bucket.Dequeue();
        }
    }

    private static string NormalizeIdentity(string identity)
    {
        var normalized = identity.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException("Checkout velocity identity is required.", nameof(identity));
        }

        return normalized;
    }
}

public sealed record CheckoutVelocityLimitResult(
    bool Allowed,
    DateTimeOffset? RetryAt,
    TimeSpan? RetryAfter,
    int MaxSessions,
    TimeSpan Window)
{
    public static CheckoutVelocityLimitResult Accepted(int maxSessions, TimeSpan window) =>
        new(true, null, null, maxSessions, window);

    public static CheckoutVelocityLimitResult Rejected(
        DateTimeOffset retryAt,
        TimeSpan retryAfter,
        int maxSessions,
        TimeSpan window) =>
        new(false, retryAt, retryAfter > TimeSpan.Zero ? retryAfter : TimeSpan.Zero, maxSessions, window);
}
