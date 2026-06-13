namespace ReplyInMyVoice.Infrastructure.Providers;

// This capture is an implementation detail of the RewriteProviderEngineClient adapter path only.
// Engines implementing IRewriteEngineClient directly must populate RewriteEngineResult.ProviderCalls
// explicitly; RewriteCostLogger writes no row when ProviderCalls is empty.
internal sealed record RewriteProviderCallMetric(
    string Provider,
    string Role,
    string? Model,
    int? InputTokens,
    int? OutputTokens,
    int? Characters,
    int? LatencyMs,
    bool Success,
    string? ErrorCode);

internal static class RewriteProviderCallCapture
{
    private static readonly AsyncLocal<Collector?> Current = new();

    public static Collector Begin()
    {
        var collector = new Collector(Current.Value);
        Current.Value = collector;
        return collector;
    }

    public static void Record(RewriteProviderCallMetric metric)
    {
        Current.Value?.Add(metric);
    }

    internal sealed class Collector(Collector? previous) : IDisposable
    {
        private readonly List<RewriteProviderCallMetric> _calls = [];
        private bool _disposed;

        public IReadOnlyList<RewriteProviderCallMetric> Calls => _calls.ToArray();

        public void Add(RewriteProviderCallMetric metric)
        {
            if (!_disposed)
            {
                _calls.Add(metric);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            Current.Value = previous;
            _disposed = true;
        }
    }
}
