using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Infrastructure.Resilience;
using ReplyInMyVoice.Tests.TestDoubles;

namespace ReplyInMyVoice.Tests;

public sealed class ProviderHttpResilienceHandlerTests
{
    [Fact]
    public async Task Opens_circuit_and_records_open_metric_once_per_transition()
    {
        var now = DateTimeOffset.Parse("2026-06-13T00:00:00Z");
        var metrics = new RecordingBusinessMetrics();
        var breaker = CreateBreaker(
            new ProviderCircuitBreakerOptions(
                TimeSpan.FromSeconds(30),
                TimeSpan.FromSeconds(30),
                1,
                1.0),
            () => now,
            new BusinessMetricsProviderResilienceEvents(metrics));
        var innerHandler = new CountingHttpHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));
        using var invoker = CreateInvoker(new ProviderHttpResilienceHandler(breaker), innerHandler);

        using var response = await invoker.SendAsync(new HttpRequestMessage(HttpMethod.Get, "https://example.test/"), CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        metrics.Records.Should().ContainSingle(record =>
            record.Name == BusinessMetricNames.ProviderBreakerOpenTotal &&
            record.Value == 1 &&
            record.DimensionName == BusinessMetricDimensions.ClientName &&
            record.DimensionValue == "test-provider");
    }

    [Fact]
    public async Task Open_circuit_rejects_requests_without_recording_additional_open_metric()
    {
        var now = DateTimeOffset.Parse("2026-06-13T00:00:00Z");
        var metrics = new RecordingBusinessMetrics();
        var breaker = CreateBreaker(
            new ProviderCircuitBreakerOptions(
                TimeSpan.FromSeconds(30),
                TimeSpan.FromSeconds(30),
                1,
                1.0),
            () => now,
            new BusinessMetricsProviderResilienceEvents(metrics));
        var innerHandler = new CountingHttpHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));
        using var invoker = CreateInvoker(new ProviderHttpResilienceHandler(breaker), innerHandler);

        using var response = await invoker.SendAsync(new HttpRequestMessage(HttpMethod.Get, "https://example.test/"), CancellationToken.None);
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        await invoker.Invoking(x => x.SendAsync(new HttpRequestMessage(HttpMethod.Get, "https://example.test/"), CancellationToken.None))
            .Should()
            .ThrowAsync<ProviderCircuitOpenException>();

        metrics.Records.Should().ContainSingle(record =>
            record.Name == BusinessMetricNames.ProviderBreakerOpenTotal);
    }

    [Fact]
    public async Task Successful_responses_do_not_record_open_metric()
    {
        var now = DateTimeOffset.Parse("2026-06-13T00:00:00Z");
        var metrics = new RecordingBusinessMetrics();
        var breaker = CreateBreaker(
            new ProviderCircuitBreakerOptions(
                TimeSpan.FromSeconds(30),
                TimeSpan.FromSeconds(30),
                2,
                1.0),
            () => now,
            new BusinessMetricsProviderResilienceEvents(metrics));
        var innerHandler = new CountingHttpHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        using var invoker = CreateInvoker(new ProviderHttpResilienceHandler(breaker), innerHandler);

        using var first = await invoker.SendAsync(new HttpRequestMessage(HttpMethod.Get, "https://example.test/"), CancellationToken.None);
        using var second = await invoker.SendAsync(new HttpRequestMessage(HttpMethod.Get, "https://example.test/"), CancellationToken.None);

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        metrics.Records.Should().BeEmpty();
    }

    [Fact]
    public async Task Circuit_state_is_shared_across_handler_instances()
    {
        var now = DateTimeOffset.Parse("2026-06-13T00:00:00Z");
        var options = new ProviderCircuitBreakerOptions(
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(30),
            1,
            1.0);
        var registry = CreateRegistry(options, () => now);
        var breaker = registry.GetOrAdd("provider-a");
        var failingInnerHandler = new CountingHttpHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));
        var fastFailInnerHandler = new CountingHttpHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

        using var firstInvoker = CreateInvoker(new ProviderHttpResilienceHandler(breaker), failingInnerHandler);
        using var secondInvoker = CreateInvoker(new ProviderHttpResilienceHandler(registry.GetOrAdd("provider-a")), fastFailInnerHandler);

        using var response = await firstInvoker.SendAsync(new HttpRequestMessage(HttpMethod.Get, "https://example.test/"), CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        breaker.State.Should().Be(ProviderCircuitState.Open);
        await secondInvoker.Invoking(x => x.SendAsync(new HttpRequestMessage(HttpMethod.Get, "https://example.test/"), CancellationToken.None))
            .Should()
            .ThrowAsync<ProviderCircuitOpenException>();
        fastFailInnerHandler.InvocationCount.Should().Be(0);
    }

    [Fact]
    public async Task Caller_cancellation_is_recorded_as_circuit_failure()
    {
        var now = DateTimeOffset.Parse("2026-06-13T00:00:00Z");
        var options = new ProviderCircuitBreakerOptions(
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(30),
            2,
            1.0);
        var breaker = CreateBreaker(options, () => now);
        using var invoker = CreateInvoker(
            new ProviderHttpResilienceHandler(breaker),
            new CountingHttpHandler((_, cancellationToken) =>
                Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken)
                    .ContinueWith<HttpResponseMessage>(
                        _ => throw new OperationCanceledException(cancellationToken),
                        cancellationToken)));

        for (var i = 0; i < options.MinimumThroughput; i++)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
            await invoker.Invoking(x => x.SendAsync(new HttpRequestMessage(HttpMethod.Get, "https://example.test/"), cts.Token))
                .Should()
                .ThrowAsync<OperationCanceledException>();
        }

        breaker.State.Should().Be(ProviderCircuitState.Open);
    }

    [Fact]
    public async Task Half_open_probe_does_not_retry_transient_failures()
    {
        var now = DateTimeOffset.Parse("2026-06-13T00:00:00Z");
        var options = new ProviderCircuitBreakerOptions(
            TimeSpan.FromSeconds(30),
            TimeSpan.FromMilliseconds(50),
            1,
            1.0);
        var breaker = CreateBreaker(options, () => now);
        breaker.Record(breaker.Acquire(), success: false);
        now = now.Add(options.BreakDuration).AddMilliseconds(1);
        var innerHandler = new CountingHttpHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));
        using var invoker = CreateInvoker(new ProviderHttpResilienceHandler(breaker), innerHandler);

        using var response = await invoker.SendAsync(new HttpRequestMessage(HttpMethod.Get, "https://example.test/"), CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        innerHandler.InvocationCount.Should().Be(1);
        breaker.State.Should().Be(ProviderCircuitState.Open);
    }

    [Fact]
    public async Task Retry_after_header_still_honored_when_circuit_closed()
    {
        var attemptTimes = new List<DateTimeOffset>();
        var attemptCount = 0;
        var breaker = CreateBreaker(
            new ProviderCircuitBreakerOptions(
                TimeSpan.FromSeconds(30),
                TimeSpan.FromSeconds(30),
                8,
                0.5),
            () => DateTimeOffset.UtcNow);
        var innerHandler = new CountingHttpHandler(_ =>
        {
            attemptCount++;
            attemptTimes.Add(DateTimeOffset.UtcNow);
            if (attemptCount == 1)
            {
                var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromMilliseconds(500));
                return Task.FromResult(response);
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });
        using var invoker = CreateInvoker(new ProviderHttpResilienceHandler(breaker), innerHandler);

        using var response = await invoker.SendAsync(new HttpRequestMessage(HttpMethod.Get, "https://example.test/"), CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        innerHandler.InvocationCount.Should().Be(2);
        attemptTimes[1].Should().BeOnOrAfter(attemptTimes[0].AddMilliseconds(500));
    }

    private static ProviderCircuitBreakerRegistry CreateRegistry(
        ProviderCircuitBreakerOptions options,
        Func<DateTimeOffset> clock) =>
        new(
            options,
            NullLoggerFactory.Instance,
            new NoOpProviderResilienceEvents(),
            clock);

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

    private static HttpMessageInvoker CreateInvoker(
        ProviderHttpResilienceHandler handler,
        HttpMessageHandler innerHandler)
    {
        handler.InnerHandler = innerHandler;
        return new HttpMessageInvoker(handler);
    }

    private sealed class CountingHttpHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        public CountingHttpHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
            : this((request, _) => handler(request))
        {
        }

        public CountingHttpHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) =>
            _handler = handler;

        public int InvocationCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            InvocationCount++;
            return _handler(request, cancellationToken);
        }
    }
}
