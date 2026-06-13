using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using ReplyInMyVoice.Infrastructure.Resilience;

namespace ReplyInMyVoice.Tests;

public sealed class ProviderCircuitBreakerTests
{
    private static readonly ProviderCircuitBreakerOptions DefaultOptions = new(
        TimeSpan.FromSeconds(30),
        TimeSpan.FromSeconds(30),
        8,
        0.5);

    [Fact]
    public void Circuit_stays_closed_below_minimum_throughput()
    {
        var now = DateTimeOffset.Parse("2026-06-13T00:00:00Z");
        var breaker = CreateBreaker(DefaultOptions, () => now);

        for (var i = 0; i < 7; i++)
        {
            breaker.Record(breaker.Acquire(), success: false);
        }

        breaker.State.Should().Be(ProviderCircuitState.Closed);
        breaker.Invoking(x => x.Acquire()).Should().NotThrow();
    }

    [Fact]
    public void Circuit_opens_at_failure_ratio_and_fails_fast()
    {
        var now = DateTimeOffset.Parse("2026-06-13T00:00:00Z");
        var breaker = CreateBreaker(DefaultOptions, () => now);

        RecordOutcomes(breaker, false, false, false, false, true, true, true, true);

        breaker.State.Should().Be(ProviderCircuitState.Open);
        var exception = breaker.Invoking(x => x.Acquire())
            .Should()
            .Throw<ProviderCircuitOpenException>()
            .Which;
        exception.ProviderName.Should().Be("test-provider");
        exception.Should().BeAssignableTo<HttpRequestException>();
    }

    [Fact]
    public void Samples_older_than_sampling_window_are_evicted()
    {
        var now = DateTimeOffset.Parse("2026-06-13T00:00:00Z");
        var options = DefaultOptions with { MinimumThroughput = 9 };
        var breaker = CreateBreaker(options, () => now);

        RecordOutcomes(breaker, false, false, false, false, false, false, false, false);
        now = now.Add(options.SamplingDuration).AddMilliseconds(1);
        breaker.Record(breaker.Acquire(), success: true);

        breaker.State.Should().Be(ProviderCircuitState.Closed);
    }

    [Fact]
    public void Open_circuit_grants_single_probe_after_break_duration()
    {
        var now = DateTimeOffset.Parse("2026-06-13T00:00:00Z");
        var options = DefaultOptions with { MinimumThroughput = 1, FailureRatio = 1.0 };
        var breaker = CreateBreaker(options, () => now);

        breaker.Record(breaker.Acquire(), success: false);
        now = now.Add(options.BreakDuration).AddMilliseconds(1);

        var lease = breaker.Acquire();

        lease.IsProbe.Should().BeTrue();
        breaker.State.Should().Be(ProviderCircuitState.HalfOpen);
        breaker.Invoking(x => x.Acquire()).Should().Throw<ProviderCircuitOpenException>();
    }

    [Fact]
    public void Half_open_probe_success_closes_circuit()
    {
        var now = DateTimeOffset.Parse("2026-06-13T00:00:00Z");
        var options = DefaultOptions with { MinimumThroughput = 1, FailureRatio = 1.0 };
        var breaker = CreateBreaker(options, () => now);

        breaker.Record(breaker.Acquire(), success: false);
        now = now.Add(options.BreakDuration).AddMilliseconds(1);
        var probe = breaker.Acquire();
        breaker.Record(probe, success: true);

        breaker.State.Should().Be(ProviderCircuitState.Closed);
        breaker.Invoking(x => x.Acquire()).Should().NotThrow();
        breaker.Record(breaker.Acquire(), success: true);
        breaker.State.Should().Be(ProviderCircuitState.Closed);
    }

    [Fact]
    public void Half_open_probe_failure_reopens_circuit()
    {
        var now = DateTimeOffset.Parse("2026-06-13T00:00:00Z");
        var options = DefaultOptions with { MinimumThroughput = 1, FailureRatio = 1.0 };
        var breaker = CreateBreaker(options, () => now);

        breaker.Record(breaker.Acquire(), success: false);
        now = now.Add(options.BreakDuration).AddMilliseconds(1);
        var probe = breaker.Acquire();
        breaker.Record(probe, success: false);

        breaker.State.Should().Be(ProviderCircuitState.Open);
        breaker.Invoking(x => x.Acquire()).Should().Throw<ProviderCircuitOpenException>();
        now = now.Add(options.BreakDuration).AddMilliseconds(1);
        breaker.Acquire().IsProbe.Should().BeTrue();
    }

    [Fact]
    public void Stuck_probe_lease_expires_and_allows_new_probe()
    {
        var now = DateTimeOffset.Parse("2026-06-13T00:00:00Z");
        var options = DefaultOptions with { MinimumThroughput = 1, FailureRatio = 1.0 };
        var breaker = CreateBreaker(options, () => now);

        breaker.Record(breaker.Acquire(), success: false);
        now = now.Add(options.BreakDuration).AddMilliseconds(1);
        breaker.Acquire().IsProbe.Should().BeTrue();
        now = now.Add(options.BreakDuration).AddMilliseconds(1);

        breaker.Acquire().IsProbe.Should().BeTrue();
    }

    [Fact]
    public void State_transitions_emit_events()
    {
        var now = DateTimeOffset.Parse("2026-06-13T00:00:00Z");
        var options = DefaultOptions with { MinimumThroughput = 1, FailureRatio = 1.0 };
        var events = new RecordingProviderResilienceEvents();
        var breaker = CreateBreaker(options, () => now, events);

        breaker.Record(breaker.Acquire(), success: false);
        now = now.Add(options.BreakDuration).AddMilliseconds(1);
        var successProbe = breaker.Acquire();
        breaker.Record(successProbe, success: true);
        breaker.Record(breaker.Acquire(), success: false);
        now = now.Add(options.BreakDuration).AddMilliseconds(1);
        var failureProbe = breaker.Acquire();
        breaker.Record(failureProbe, success: false);

        events.Transitions.Should().Contain(("test-provider", ProviderCircuitState.Closed, ProviderCircuitState.Open, 1, 1));
        events.Transitions.Should().Contain(("test-provider", ProviderCircuitState.Open, ProviderCircuitState.HalfOpen, 0, 0));
        events.Transitions.Should().Contain(("test-provider", ProviderCircuitState.HalfOpen, ProviderCircuitState.Closed, 0, 0));
        events.Transitions.Should().Contain(("test-provider", ProviderCircuitState.HalfOpen, ProviderCircuitState.Open, 0, 0));
        ProviderResilienceEventNames.CircuitStateChanged.Should().Be("provider_circuit_state_change");
    }

    private static ProviderCircuitBreaker CreateBreaker(
        ProviderCircuitBreakerOptions options,
        Func<DateTimeOffset> clock,
        IProviderResilienceEvents? events = null) =>
        new(
            "test-provider",
            options,
            NullLogger.Instance,
            events ?? new NoOpProviderResilienceEvents(),
            clock);

    private static void RecordOutcomes(ProviderCircuitBreaker breaker, params bool[] outcomes)
    {
        foreach (var outcome in outcomes)
        {
            breaker.Record(breaker.Acquire(), outcome);
        }
    }

    private sealed class RecordingProviderResilienceEvents : IProviderResilienceEvents
    {
        public List<(string ProviderName, ProviderCircuitState From, ProviderCircuitState To, int Failures, int Total)> Transitions { get; } = [];

        public void CircuitStateChanged(
            string providerName,
            ProviderCircuitState fromState,
            ProviderCircuitState toState,
            int sampledFailures,
            int sampledTotal) =>
            Transitions.Add((providerName, fromState, toState, sampledFailures, sampledTotal));
    }
}
