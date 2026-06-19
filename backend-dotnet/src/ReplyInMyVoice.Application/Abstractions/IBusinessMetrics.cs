namespace ReplyInMyVoice.Application.Abstractions;

public interface IBusinessMetrics
{
    void Record(string metricName, double value);

    void Record(string metricName, double value, string dimensionName, string dimensionValue);

    void Record(
        string metricName,
        double value,
        string firstDimensionName,
        string firstDimensionValue,
        string secondDimensionName,
        string secondDimensionValue);
}

public sealed class NoOpBusinessMetrics : IBusinessMetrics
{
    public static readonly NoOpBusinessMetrics Instance = new();

    private NoOpBusinessMetrics()
    {
    }

    public void Record(string metricName, double value)
    {
    }

    public void Record(string metricName, double value, string dimensionName, string dimensionValue)
    {
    }

    public void Record(
        string metricName,
        double value,
        string firstDimensionName,
        string firstDimensionValue,
        string secondDimensionName,
        string secondDimensionValue)
    {
    }
}

public static class BusinessMetricNames
{
    /// <summary>No dimensions.</summary>
    public const string OutboxBacklogAgeSeconds = "outbox_backlog_age_seconds";

    /// <summary>Dimension: message_type.</summary>
    public const string OutboxFailedTotal = "outbox_failed_total";

    /// <summary>Dimension: event_type.</summary>
    public const string StripeEventFailedTotal = "stripe_event_failed_total";

    /// <summary>No dimensions.</summary>
    public const string WebhookProcessingLagSeconds = "webhook_processing_lag_seconds";

    /// <summary>Dimension: api_key_id.</summary>
    public const string WebhookFailurePerApiKeyTotal = "webhook_failure_per_api_key_total";

    /// <summary>Dimensions: api_key_id, terminal_reason.</summary>
    public const string WebhookDeliveryConsecutiveFailureTotal = "webhook_delivery_consecutive_failure_total";

    /// <summary>Dimension: error_code.</summary>
    public const string RewriteQualityFailureTotal = "rewrite_quality_failure_total";

    /// <summary>Dimension: error_code.</summary>
    public const string QuotaReleasedTotal = "quota_released_total";

    /// <summary>Dimension: client_name.</summary>
    public const string ProviderBreakerOpenTotal = "provider_breaker_open_total";
}

public static class BusinessMetricDimensions
{
    public const string MessageType = "message_type";
    public const string EventType = "event_type";
    public const string ErrorCode = "error_code";
    public const string ClientName = "client_name";
    public const string ApiKeyId = "api_key_id";
    public const string TerminalReason = "terminal_reason";
}
