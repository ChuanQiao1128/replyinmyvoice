namespace ReplyInMyVoice.Infrastructure.Resilience;

public sealed record ProviderCircuitBreakerOptions(
    TimeSpan SamplingDuration,
    TimeSpan BreakDuration,
    int MinimumThroughput,
    double FailureRatio);
