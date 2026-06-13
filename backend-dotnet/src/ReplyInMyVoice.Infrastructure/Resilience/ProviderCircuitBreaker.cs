using Microsoft.Extensions.Logging;

namespace ReplyInMyVoice.Infrastructure.Resilience;

public enum ProviderCircuitState
{
    Closed,
    Open,
    HalfOpen,
}

public sealed class ProviderCircuitOpenException : HttpRequestException
{
    public ProviderCircuitOpenException(string providerName)
        : base($"Provider HTTP circuit is open for {providerName}.") =>
        ProviderName = providerName;

    public string ProviderName { get; }
}

public readonly record struct CircuitLease(bool IsProbe);

public sealed class ProviderCircuitBreaker
{
    private readonly string _providerName;
    private readonly ProviderCircuitBreakerOptions _options;
    private readonly ILogger _logger;
    private readonly IProviderResilienceEvents _events;
    private readonly Func<DateTimeOffset> _clock;
    private readonly object _lock = new();
    private readonly Queue<CircuitSample> _samples = new();

    private ProviderCircuitState _state = ProviderCircuitState.Closed;
    private DateTimeOffset? _openUntil;
    private DateTimeOffset? _probeStartedAt;

    public ProviderCircuitBreaker(
        string providerName,
        ProviderCircuitBreakerOptions options,
        ILogger logger,
        IProviderResilienceEvents events,
        Func<DateTimeOffset>? clock = null)
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            throw new ArgumentException("Provider name is required.", nameof(providerName));
        }

        _providerName = providerName;
        _options = options;
        _logger = logger;
        _events = events;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public ProviderCircuitState State
    {
        get
        {
            lock (_lock)
            {
                return _state;
            }
        }
    }

    public CircuitLease Acquire()
    {
        var now = _clock();
        lock (_lock)
        {
            if (_state == ProviderCircuitState.Closed)
            {
                return new CircuitLease(IsProbe: false);
            }

            if (_state == ProviderCircuitState.Open)
            {
                if (_openUntil is not null && now < _openUntil.Value)
                {
                    throw new ProviderCircuitOpenException(_providerName);
                }

                TransitionTo(ProviderCircuitState.HalfOpen, sampledFailures: 0, sampledTotal: 0);
                _probeStartedAt = now;
                return new CircuitLease(IsProbe: true);
            }

            if (_probeStartedAt is null || now - _probeStartedAt.Value > _options.BreakDuration)
            {
                _probeStartedAt = now;
                return new CircuitLease(IsProbe: true);
            }

            throw new ProviderCircuitOpenException(_providerName);
        }
    }

    public void ThrowIfOpen()
    {
        var now = _clock();
        lock (_lock)
        {
            if (_state == ProviderCircuitState.Open &&
                _openUntil is not null &&
                now < _openUntil.Value)
            {
                throw new ProviderCircuitOpenException(_providerName);
            }

            if (_state == ProviderCircuitState.HalfOpen &&
                _probeStartedAt is not null &&
                now - _probeStartedAt.Value <= _options.BreakDuration)
            {
                throw new ProviderCircuitOpenException(_providerName);
            }
        }
    }

    public void Record(CircuitLease lease, bool success)
    {
        var now = _clock();
        lock (_lock)
        {
            if (lease.IsProbe)
            {
                RecordProbeResult(success, now);
                return;
            }

            if (_state != ProviderCircuitState.Closed)
            {
                return;
            }

            EvictExpiredSamples(now);
            _samples.Enqueue(new CircuitSample(now, success));
            if (_samples.Count < _options.MinimumThroughput)
            {
                return;
            }

            var failures = _samples.Count(sample => !sample.Success);
            if ((double)failures / _samples.Count >= _options.FailureRatio)
            {
                _openUntil = now + _options.BreakDuration;
                TransitionTo(ProviderCircuitState.Open, failures, _samples.Count);
            }
        }
    }

    private void RecordProbeResult(bool success, DateTimeOffset now)
    {
        _probeStartedAt = null;
        if (success)
        {
            _samples.Clear();
            _openUntil = null;
            TransitionTo(ProviderCircuitState.Closed, sampledFailures: 0, sampledTotal: 0);
            return;
        }

        _openUntil = now + _options.BreakDuration;
        TransitionTo(ProviderCircuitState.Open, sampledFailures: 0, sampledTotal: 0);
    }

    private void EvictExpiredSamples(DateTimeOffset now)
    {
        while (_samples.Count > 0 &&
               now - _samples.Peek().ObservedAt > _options.SamplingDuration)
        {
            _samples.Dequeue();
        }
    }

    private void TransitionTo(
        ProviderCircuitState toState,
        int sampledFailures,
        int sampledTotal)
    {
        var fromState = _state;
        if (fromState == toState)
        {
            return;
        }

        _state = toState;
        if (toState == ProviderCircuitState.Open)
        {
            _logger.LogWarning(
                "Provider circuit opened for {Provider}: {Failures}/{Total} failures in sampling window; breaking for {BreakSeconds}s.",
                _providerName,
                sampledFailures,
                sampledTotal,
                _options.BreakDuration.TotalSeconds);
        }
        else
        {
            _logger.LogInformation(
                "Provider circuit state changed for {Provider}: {FromState} -> {ToState}.",
                _providerName,
                fromState,
                toState);
        }

        _events.CircuitStateChanged(
            _providerName,
            fromState,
            toState,
            sampledFailures,
            sampledTotal);
    }

    private readonly record struct CircuitSample(DateTimeOffset ObservedAt, bool Success);
}
