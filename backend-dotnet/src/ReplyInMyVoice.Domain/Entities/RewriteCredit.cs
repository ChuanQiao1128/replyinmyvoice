namespace ReplyInMyVoice.Domain.Entities;

public sealed class RewriteCredit
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public AppUser? User { get; set; }
    public required string Source { get; set; }
    public int AmountGranted { get; set; }
    public int? OriginalAmountGranted { get; set; }
    public int AmountConsumed { get; set; }
    public DateTimeOffset GrantedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ExpiresAt { get; set; }
    public DateTimeOffset? ExpiryReminderSentAt { get; set; }
    public string? StripeEventId { get; set; }
    public string? StripePaymentIntentId { get; set; }
    public string? StripeReceiptUrl { get; set; }
    public string? StripeSku { get; set; }
    public long? StripeAmountTotal { get; set; }
    public string? StripeCurrency { get; set; }
    public Guid RowVersion { get; set; } = Guid.NewGuid();
}
