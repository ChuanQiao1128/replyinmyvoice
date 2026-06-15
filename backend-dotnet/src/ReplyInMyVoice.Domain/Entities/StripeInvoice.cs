using ReplyInMyVoice.Domain.Contracts;

namespace ReplyInMyVoice.Domain.Entities;

public sealed class StripeInvoice : IConcurrencyStamped
{
    public required string Id { get; set; }
    public Guid UserId { get; set; }
    public AppUser? User { get; set; }
    public string? SubscriptionId { get; set; }
    public required string Status { get; set; }
    public long AmountDue { get; set; }
    public long AmountPaid { get; set; }
    public required string Currency { get; set; }
    public DateTimeOffset? PeriodStart { get; set; }
    public DateTimeOffset? PeriodEnd { get; set; }
    public int AttemptCount { get; set; }
    public DateTimeOffset? NextPaymentAttempt { get; set; }
    public string? HostedInvoiceUrl { get; set; }
    public string? InvoicePdf { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid RowVersion { get; set; } = Guid.NewGuid();
}
