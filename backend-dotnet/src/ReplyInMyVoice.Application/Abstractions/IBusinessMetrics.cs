namespace ReplyInMyVoice.Application.Abstractions;

public interface IBusinessMetrics
{
    void Record(string metricName, double value);

    void Record(string metricName, double value, string dimensionName, string dimensionValue);
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

    /// <summary>Dimension: error_code.</summary>
    public const string RewriteQualityFailureTotal = "rewrite_quality_failure_total";

    /// <summary>Dimension: error_code.</summary>
    public const string QuotaReleasedTotal = "quota_released_total";

    /// <summary>Dimension: client_name.</summary>
    public const string ProviderBreakerOpenTotal = "provider_breaker_open_total";

    /// <summary>Dimension: reason (exception type or error category).</summary>
    public const string StuckReservationsCleanupFailedTotal = "stuck_reservations_cleanup_failed_total";

    /// <summary>Dimension: api_key_id.</summary>
    public const string WebhookDeliveryFailedTotal = "webhook_delivery_failed_total";

    /// <summary>Dimension: api_key_id.</summary>
    public const string WebhookDeliveryBacklog = "webhook_delivery_backlog";
}

public static class BusinessMetricDimensions
{
    public const string MessageType = "message_type";
    public const string EventType = "event_type";
    public const string ErrorCode = "error_code";
    public const string ClientName = "client_name";
    public const string Reason = "reason";
    public const string ApiKeyId = "api_key_id";
}
