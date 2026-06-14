namespace ReplyInMyVoice.Infrastructure.Resilience;

public interface IProviderResilienceEvents
{
    void CircuitStateChanged(
        string providerName,
        ProviderCircuitState fromState,
        ProviderCircuitState toState,
        int sampledFailures,
        int sampledTotal);
}

public sealed class NoOpProviderResilienceEvents : IProviderResilienceEvents
{
    public void CircuitStateChanged(
        string providerName,
        ProviderCircuitState fromState,
        ProviderCircuitState toState,
        int sampledFailures,
        int sampledTotal)
    {
    }
}

public static class ProviderResilienceEventNames
{
    public const string CircuitStateChanged = "provider_circuit_state_change";
}
