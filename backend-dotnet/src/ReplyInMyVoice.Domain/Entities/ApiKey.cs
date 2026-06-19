namespace ReplyInMyVoice.Domain.Entities;

public sealed class ApiKey
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public AppUser? User { get; set; }
    public required string KeyHash { get; set; }
    public string? Last4 { get; set; }
    public required string Name { get; set; }
    public string PlanTier { get; set; } = "free";
    public bool IsTest { get; set; }
    public int PepperVersion { get; set; } = 1;
    /// <summary>
    /// Set after successful authentication with an old pepper version; a later rehash updates KeyHash.
    /// </summary>
    public bool RehashPending { get; set; }
    public int RateLimitPerMinute { get; set; } = 60;
    public int MonthlyQuota { get; set; } = 1000;
    /// <summary>
    /// Legacy denormalized counter retained only for additive-only schema stability.
    /// </summary>
    [Obsolete("Legacy denormalized counter that is never written. Per-key usage is computed from ApiKeyUsage rows (ApiKeyUsageRepository.CountByApiKeyAsync) and per-minute rate limiting from ApiKeyRateLimitWindow (ApiKeyRateLimiter). Column retained only for additive-only schema stability; do not read or write.")]
    public int CurrentPeriodUsage { get; set; }
    public DateTimeOffset CurrentPeriodStartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastUsedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public string? WebhookUrl { get; set; }
    public string? WebhookSecret { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    // Client-managed Guid concurrency token: every ApiKey writer must assign a new Guid before saving.
    public Guid RowVersion { get; set; } = Guid.NewGuid();

    public ICollection<ApiKeyUsage> ApiKeyUsages { get; } = new List<ApiKeyUsage>();
    public ICollection<ApiKeyRateLimitWindow> RateLimitWindows { get; } = new List<ApiKeyRateLimitWindow>();
    public ICollection<WebhookDelivery> WebhookDeliveries { get; } = new List<WebhookDelivery>();
}
