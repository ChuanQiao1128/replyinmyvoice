using System.Diagnostics;

namespace ReplyInMyVoice.Infrastructure.Observability;

public sealed class DistributedTracingActivityScope : IDisposable
{
    private readonly Activity? _previous;
    private bool _disposed;

    internal DistributedTracingActivityScope(Activity? previous, Activity? activity)
    {
        _previous = previous;
        Activity = activity;
    }

    public Activity? Activity { get; }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Activity?.Dispose();
        Activity.Current = _previous;
        _disposed = true;
    }
}
