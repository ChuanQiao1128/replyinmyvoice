using FluentAssertions;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Infrastructure.Services;

namespace ReplyInMyVoice.Tests;

public sealed class BusinessMetricsTests
{
    [Fact]
    public void AppInsightsBusinessMetrics_sends_metric_telemetry_through_spy_channel()
    {
        using var configuration = CreateTelemetryConfiguration(out var channel);
        var telemetryClient = new TelemetryClient(configuration);
        var metrics = new AppInsightsBusinessMetrics(telemetryClient);

        metrics.Record(BusinessMetricNames.OutboxBacklogAgeSeconds, 42);
        telemetryClient.Flush();

        var metric = channel.Telemetry.OfType<MetricTelemetry>().Should().ContainSingle().Subject;
        metric.Name.Should().Be(BusinessMetricNames.OutboxBacklogAgeSeconds);
        metric.Sum.Should().Be(42);
        metric.Count.Should().Be(1);
    }

    [Fact]
    public void AppInsightsBusinessMetrics_sends_dimension_as_custom_property()
    {
        using var configuration = CreateTelemetryConfiguration(out var channel);
        var telemetryClient = new TelemetryClient(configuration);
        var metrics = new AppInsightsBusinessMetrics(telemetryClient);

        metrics.Record(
            BusinessMetricNames.QuotaReleasedTotal,
            1,
            BusinessMetricDimensions.ErrorCode,
            "provider_failed");
        telemetryClient.Flush();

        var metric = channel.Telemetry.OfType<MetricTelemetry>().Should().ContainSingle().Subject;
        metric.Name.Should().Be(BusinessMetricNames.QuotaReleasedTotal);
        metric.Sum.Should().Be(1);
        metric.Count.Should().Be(1);
        metric.Properties[BusinessMetricDimensions.ErrorCode].Should().Be("provider_failed");
    }

    [Fact]
    public void AppInsightsBusinessMetrics_never_throws_when_metric_identity_conflicts()
    {
        using var configuration = CreateTelemetryConfiguration(out _);
        var telemetryClient = new TelemetryClient(configuration);
        var metrics = new AppInsightsBusinessMetrics(telemetryClient);

        var act = () =>
        {
            metrics.Record("metric_identity_conflict_test", 1);
            metrics.Record("metric_identity_conflict_test", 1, "kind", "value");
        };

        act.Should().NotThrow();
    }

    private static TelemetryConfiguration CreateTelemetryConfiguration(out SpyTelemetryChannel channel)
    {
        channel = new SpyTelemetryChannel();
        return new TelemetryConfiguration
        {
            TelemetryChannel = channel,
            ConnectionString = "InstrumentationKey=00000000-0000-0000-0000-000000000000",
        };
    }

    private sealed class SpyTelemetryChannel : ITelemetryChannel
    {
        private readonly List<ITelemetry> _telemetry = [];

        public IReadOnlyList<ITelemetry> Telemetry => _telemetry;

        public bool? DeveloperMode { get; set; }

        public string? EndpointAddress { get; set; }

        public void Send(ITelemetry item) => _telemetry.Add(item);

        public void Flush()
        {
        }

        public void Dispose()
        {
        }
    }
}
