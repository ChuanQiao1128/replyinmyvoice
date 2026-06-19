using System.Diagnostics;
using Azure.Messaging.ServiceBus;
using FluentAssertions;
using ReplyInMyVoice.Domain.Contracts;
using ReplyInMyVoice.Infrastructure.Observability;
using ReplyInMyVoice.Infrastructure.Queueing;

namespace ReplyInMyVoice.Tests;

public sealed class DistributedTracingE2ETests
{
    [Fact]
    public void Traceparent_flows_from_api_activity_to_service_bus_and_worker_activity()
    {
        using var listener = CreateListener();
        var tracing = DistributedTracingActivitySource.Instance;
        var previous = Activity.Current;
        using var apiScope = DistributedTracingContext.StartIncomingActivity(
            tracing,
            "Api.HttpRequest",
            ActivityKind.Server,
            traceparent: null,
            tracestate: null,
            enabled: true);
        apiScope.Activity.Should().NotBeNull();
        var apiActivity = apiScope.Activity!;
        var job = new RewriteJob(Guid.NewGuid());

        var message = AzureServiceBusRewriteJobPublisher.CreateMessage(job, apiActivity.Id);

        message.ApplicationProperties[DistributedTracingContext.TraceparentPropertyName]
            .Should()
            .Be(apiActivity.Id);
        message.ApplicationProperties[DistributedTracingContext.CorrelationIdPropertyName]
            .Should()
            .Be(apiActivity.Id);

        using var workerScope = DistributedTracingContext.StartServiceBusConsumerActivity(
            tracing,
            message.ApplicationProperties,
            "Worker.ServiceBusRewriteJob",
            enabled: true);
        workerScope.Activity.Should().NotBeNull();
        var workerActivity = workerScope.Activity!;

        Activity.Current.Should().BeSameAs(workerActivity);
        workerActivity.Kind.Should().Be(ActivityKind.Consumer);
        workerActivity.TraceId.Should().Be(apiActivity.TraceId);
        workerActivity.ParentSpanId.Should().Be(apiActivity.SpanId);
        workerActivity.GetTagItem("CorrelationId").Should().Be(apiActivity.Id);

        workerScope.Dispose();
        Activity.Current.Should().BeSameAs(apiActivity);
        apiScope.Dispose();
        Activity.Current.Should().BeSameAs(previous);
    }

    [Fact]
    public void Worker_activity_extracts_traceparent_from_service_bus_received_message_properties()
    {
        using var listener = CreateListener();
        var tracing = DistributedTracingActivitySource.Instance;
        using var apiActivity = tracing.ActivitySource.StartActivity("Api.HttpRequest", ActivityKind.Server);
        var received = ServiceBusModelFactory.ServiceBusReceivedMessage(
            properties: new Dictionary<string, object>
            {
                [DistributedTracingContext.TraceparentPropertyName] = apiActivity!.Id!,
                [DistributedTracingContext.CorrelationIdPropertyName] = "corr-456",
            });

        using var workerScope = DistributedTracingContext.StartServiceBusConsumerActivity(
            tracing,
            received.ApplicationProperties,
            "Worker.ServiceBusRewriteJob",
            enabled: true);

        workerScope.Activity.Should().NotBeNull();
        var workerActivity = workerScope.Activity!;
        workerActivity.TraceId.Should().Be(apiActivity.TraceId);
        workerActivity.ParentSpanId.Should().Be(apiActivity.SpanId);
        workerActivity.GetTagItem("CorrelationId").Should().Be("corr-456");
    }

    private static ActivityListener CreateListener()
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == DistributedTracingActivitySource.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }
}
