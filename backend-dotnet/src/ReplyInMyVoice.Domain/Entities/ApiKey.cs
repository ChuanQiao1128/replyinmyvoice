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
    public string Scope { get; set; } = "[]";
    public bool IsTest { get; set; }
    public int RateLimitPerMinute { get; set; } = 60;
    public int MonthlyQuota { get; set; } = 1000;
    public int CurrentPeriodUsage { get; set; }
    public DateTimeOffset CurrentPeriodStartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastUsedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public string? WebhookUrl { get; set; }
    public string? WebhookSecret { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid RowVersion { get; set; } = Guid.NewGuid();

    public ICollection<ApiKeyUsage> ApiKeyUsages { get; } = new List<ApiKeyUsage>();
    public ICollection<ApiKeyRateLimitWindow> RateLimitWindows { get; } = new List<ApiKeyRateLimitWindow>();
    public ICollection<WebhookDelivery> WebhookDeliveries { get; } = new List<WebhookDelivery>();
}
