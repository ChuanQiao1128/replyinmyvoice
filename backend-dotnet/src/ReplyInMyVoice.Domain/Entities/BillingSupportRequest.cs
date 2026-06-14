using ReplyInMyVoice.Domain.Contracts;
using ReplyInMyVoice.Domain.Enums;

namespace ReplyInMyVoice.Domain.Entities;

public sealed class BillingSupportRequest : IConcurrencyStamped
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public AppUser? User { get; set; }
    public BillingSupportRequestType Type { get; set; }
    public string? RelatedPaymentIntentId { get; set; }
    public required string Message { get; set; }
    public BillingSupportRequestStatus Status { get; set; } = BillingSupportRequestStatus.Open;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ResolvedAt { get; set; }
    public Guid RowVersion { get; set; } = Guid.NewGuid();
}
