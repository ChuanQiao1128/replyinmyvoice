using System.Diagnostics;
using Azure.Messaging.ServiceBus;

namespace ReplyInMyVoice.Infrastructure.Observability;

public static class DistributedTracingContext
{
    public const string TraceparentHeaderName = "traceparent";
    public const string TracestateHeaderName = "tracestate";
    public const string TraceparentPropertyName = "traceparent";
    public const string CorrelationIdPropertyName = "CorrelationId";

    public static DistributedTracingActivityScope StartIncomingActivity(
        DistributedTracingActivitySource tracing,
        string operationName,
        ActivityKind kind,
        string? traceparent,
        string? tracestate,
        bool enabled)
    {
        var previous = Activity.Current;
        if (!enabled)
        {
            return new DistributedTracingActivityScope(previous, activity: null);
        }

        Activity.DefaultIdFormat = ActivityIdFormat.W3C;
        Activity.ForceDefaultIdFormat = true;

        var activity = TryParseTraceparent(traceparent, tracestate, out var parentContext)
            ? tracing.ActivitySource.StartActivity(operationName, kind, parentContext)
            : tracing.ActivitySource.StartActivity(operationName, kind);

        if (activity is not null)
        {
            Activity.Current = activity;
        }

        return new DistributedTracingActivityScope(previous, activity);
    }

    public static DistributedTracingActivityScope StartServiceBusConsumerActivity(
        DistributedTracingActivitySource tracing,
        IEnumerable<KeyValuePair<string, object>> applicationProperties,
        string operationName,
        bool enabled)
    {
        var traceparent = GetApplicationPropertyValue(applicationProperties, TraceparentPropertyName);
        var scope = StartIncomingActivity(
            tracing,
            operationName,
            ActivityKind.Consumer,
            traceparent,
            tracestate: null,
            enabled);

        if (scope.Activity is { } activity)
        {
            var correlationId = GetApplicationPropertyValue(applicationProperties, CorrelationIdPropertyName);
            if (!string.IsNullOrWhiteSpace(correlationId))
            {
                activity.SetTag(CorrelationIdPropertyName, correlationId);
            }

            activity.SetTag("messaging.system", "azureservicebus");
            activity.SetTag("messaging.operation", "process");
        }

        return scope;
    }

    public static void StampServiceBusApplicationProperties(
        ServiceBusMessage message,
        string? correlationId)
    {
        var traceparent = ResolveServiceBusTraceparent(correlationId);
        if (!string.IsNullOrWhiteSpace(traceparent))
        {
            message.ApplicationProperties[TraceparentPropertyName] = traceparent;
        }

        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            message.ApplicationProperties[CorrelationIdPropertyName] = correlationId;
        }
        else if (!string.IsNullOrWhiteSpace(traceparent))
        {
            message.ApplicationProperties[CorrelationIdPropertyName] = traceparent;
        }
    }

    public static string? ResolveServiceBusTraceparent(string? correlationId) =>
        ResolveCurrentTraceparent()
            ?? (IsW3CTraceparent(correlationId) ? correlationId : null);

    public static Dictionary<string, object> BuildLogScope(string? correlationId)
    {
        var scope = new Dictionary<string, object>();
        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            scope[CorrelationIdPropertyName] = correlationId;
        }

        if (Activity.Current is { } activity)
        {
            if (!string.IsNullOrWhiteSpace(activity.Id))
            {
                scope["ActivityId"] = activity.Id;
            }

            if (!string.IsNullOrWhiteSpace(activity.RootId))
            {
                scope["ActivityRootId"] = activity.RootId;
            }
        }

        return scope;
    }

    public static bool TryParseTraceparent(
        string? traceparent,
        string? tracestate,
        out ActivityContext parentContext)
    {
        parentContext = default;
        return !string.IsNullOrWhiteSpace(traceparent) &&
            ActivityContext.TryParse(traceparent, tracestate, out parentContext);
    }

    public static bool IsW3CTraceparent(string? traceparent) =>
        TryParseTraceparent(traceparent, tracestate: null, out _);

    private static string? ResolveCurrentTraceparent()
    {
        var currentId = Activity.Current?.Id;
        return IsW3CTraceparent(currentId) ? currentId : null;
    }

    public static string? GetApplicationPropertyValue(
        IEnumerable<KeyValuePair<string, object>> applicationProperties,
        string name)
    {
        foreach (var property in applicationProperties)
        {
            if (!string.Equals(property.Key, name, StringComparison.Ordinal))
            {
                continue;
            }

            return property.Value switch
            {
                string text => text,
                BinaryData binaryData => binaryData.ToString(),
                null => null,
                _ => Convert.ToString(property.Value),
            };
        }

        return null;
    }
}
