using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace ReplyInMyVoice.Infrastructure.Resilience;

public sealed class ProviderCircuitBreakerRegistry
{
    private readonly ProviderCircuitBreakerOptions _options;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IProviderResilienceEvents _events;
    private readonly Func<DateTimeOffset>? _clock;
    private readonly ConcurrentDictionary<string, Lazy<ProviderCircuitBreaker>> _breakers =
        new(StringComparer.Ordinal);

    public ProviderCircuitBreakerRegistry(
        ProviderCircuitBreakerOptions options,
        ILoggerFactory loggerFactory,
        IProviderResilienceEvents events,
        Func<DateTimeOffset>? clock = null)
    {
        _options = options;
        _loggerFactory = loggerFactory;
        _events = events;
        _clock = clock;
    }

    public ProviderCircuitBreaker GetOrAdd(string providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            throw new ArgumentException("Provider name is required.", nameof(providerName));
        }

        return _breakers.GetOrAdd(
            providerName,
            static (name, state) => new Lazy<ProviderCircuitBreaker>(
                () => new ProviderCircuitBreaker(
                    name,
                    state.Options,
                    state.LoggerFactory.CreateLogger<ProviderCircuitBreaker>(),
                    state.Events,
                    state.Clock),
                LazyThreadSafetyMode.ExecutionAndPublication),
            (Options: _options, LoggerFactory: _loggerFactory, Events: _events, Clock: _clock)).Value;
    }
}
