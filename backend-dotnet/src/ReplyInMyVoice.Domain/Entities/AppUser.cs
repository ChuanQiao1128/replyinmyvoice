using ReplyInMyVoice.Domain.Enums;

namespace ReplyInMyVoice.Domain.Entities;

public sealed class AppUser
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string ExternalAuthUserId { get; set; }
    public string? Email { get; set; }
    public string? StripeCustomerId { get; set; }
    public string? StripeSubscriptionId { get; set; }
    public SubscriptionStatus SubscriptionStatus { get; set; } = SubscriptionStatus.Inactive;
    public DateTimeOffset? CurrentPeriodEnd { get; set; }
    public DateTimeOffset? PaymentFailedAt { get; set; }
    public DateTimeOffset? PaymentGraceEndsAt { get; set; }
    public DateTimeOffset? SuspendedAt { get; set; }
    public DateTimeOffset? ConsentAcceptedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid RowVersion { get; set; } = Guid.NewGuid();

    public ICollection<UsagePeriod> UsagePeriods { get; } = new List<UsagePeriod>();
    public ICollection<RewriteAttempt> RewriteAttempts { get; } = new List<RewriteAttempt>();
    public ICollection<UsageReservation> UsageReservations { get; } = new List<UsageReservation>();
    public ICollection<BillingSupportRequest> BillingSupportRequests { get; } = new List<BillingSupportRequest>();
}
