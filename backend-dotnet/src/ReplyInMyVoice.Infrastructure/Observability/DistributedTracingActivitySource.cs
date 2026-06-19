using System.Diagnostics;

namespace ReplyInMyVoice.Infrastructure.Observability;

public sealed class DistributedTracingActivitySource
{
    public const string SourceName = "ReplyInMyVoice";

    public static readonly ActivitySource Source = new(SourceName);
    public static readonly DistributedTracingActivitySource Instance = new();

    private DistributedTracingActivitySource()
    {
    }

    public ActivitySource ActivitySource => Source;
}
