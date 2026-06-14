namespace ReplyInMyVoice.Domain.Entities;

public sealed class StripeReconciliationRun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset WindowStart { get; set; }
    public DateTimeOffset WindowEnd { get; set; }
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset CompletedAt { get; set; } = DateTimeOffset.UtcNow;
    public int StripePaymentCount { get; set; }
    public int PurchaseGrantCount { get; set; }
    public int PaidButNoGrantCount { get; set; }
    public int GrantButNoPaymentCount { get; set; }
    public int AmountMismatchCount { get; set; }
    public int SubscriptionMismatchCount { get; set; }
    public int AutoGrantedCount { get; set; }
    public int AutoGrantSkippedCount { get; set; }
    public int ManualReviewCount { get; set; }
    public string ReportJson { get; set; } = "{}";
    public Guid RowVersion { get; set; } = Guid.NewGuid();
}
