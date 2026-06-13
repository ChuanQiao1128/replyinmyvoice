using ReplyInMyVoice.Application.Abstractions;

namespace ReplyInMyVoice.Infrastructure.Resilience;

public sealed class BusinessMetricsProviderResilienceEvents(IBusinessMetrics metrics) : IProviderResilienceEvents
{
    public void CircuitStateChanged(
        string providerName,
        ProviderCircuitState fromState,
        ProviderCircuitState toState,
        int sampledFailures,
        int sampledTotal)
    {
        if (toState != ProviderCircuitState.Open || fromState == ProviderCircuitState.Open)
        {
            return;
        }

        metrics.Record(
            BusinessMetricNames.ProviderBreakerOpenTotal,
            1,
            BusinessMetricDimensions.ClientName,
            providerName);
    }
}
