using System.Collections.Concurrent;
using System.Diagnostics;
using Azure.Messaging.ServiceBus;
using FluentAssertions;
using ReplyInMyVoice.Domain.Contracts;
using ReplyInMyVoice.Infrastructure.Observability;
using ReplyInMyVoice.Infrastructure.Queueing;

namespace ReplyInMyVoice.Tests;

public sealed class ServiceBusTraceparentPropagationTests
{
    [Fact]
    public void CreateMessage_stamps_current_traceparent_and_correlation_id()
    {
        using var listener = CreateListener();
        var tracing = DistributedTracingActivitySource.Instance;
        using var activity = tracing.ActivitySource.StartActivity("Api.HttpRequest", ActivityKind.Server);
        activity.Should().NotBeNull();
        var job = new RewriteJob(Guid.NewGuid());

        var message = AzureServiceBusRewriteJobPublisher.CreateMessage(job, "corr-123");

        message.ApplicationProperties.Should().ContainKey(DistributedTracingContext.TraceparentPropertyName);
        message.ApplicationProperties[DistributedTracingContext.TraceparentPropertyName]
            .Should()
            .Be(activity!.Id);
        message.ApplicationProperties.Should().ContainKey(DistributedTracingContext.CorrelationIdPropertyName);
        message.ApplicationProperties[DistributedTracingContext.CorrelationIdPropertyName]
            .Should()
            .Be("corr-123");
    }

    [Fact]
    public void CreateMessage_preserves_correlation_id_without_traceparent_when_no_activity_is_current()
    {
        var previous = Activity.Current;
        Activity.Current = null;
        try
        {
            var message = AzureServiceBusRewriteJobPublisher.CreateMessage(
                new RewriteJob(Guid.NewGuid()),
                "corr-only");

            message.ApplicationProperties.Should().NotContainKey(DistributedTracingContext.TraceparentPropertyName);
            message.ApplicationProperties[DistributedTracingContext.CorrelationIdPropertyName]
                .Should()
                .Be("corr-only");
        }
        finally
        {
            Activity.Current = previous;
        }
    }

    [Fact]
    public void CreateMessage_uses_w3c_correlation_id_as_traceparent_when_current_activity_is_missing()
    {
        using var listener = CreateListener();
        var tracing = DistributedTracingActivitySource.Instance;
        using var activity = tracing.ActivitySource.StartActivity("Api.HttpRequest", ActivityKind.Server);
        var traceparent = activity!.Id;
        activity.Dispose();

        var message = AzureServiceBusRewriteJobPublisher.CreateMessage(
            new RewriteJob(Guid.NewGuid()),
            traceparent);

        message.ApplicationProperties[DistributedTracingContext.TraceparentPropertyName]
            .Should()
            .Be(traceparent);
        message.ApplicationProperties[DistributedTracingContext.CorrelationIdPropertyName]
            .Should()
            .Be(traceparent);
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
