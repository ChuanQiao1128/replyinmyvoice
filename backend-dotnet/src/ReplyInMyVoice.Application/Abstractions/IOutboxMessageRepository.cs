using ReplyInMyVoice.Domain.Entities;

namespace ReplyInMyVoice.Application.Abstractions;

public interface IOutboxMessageRepository
{
    Task AddAsync(OutboxMessage message, CancellationToken ct = default);

    Task<IReadOnlyList<OutboxMessage>> ClaimDueAsync(
        DateTimeOffset now,
        string lockedBy,
        int batchSize,
        TimeSpan claimLease,
        CancellationToken ct = default);

    Task<OutboxMessage?> ClaimByIdAsync(
        Guid messageId,
        DateTimeOffset now,
        string lockedBy,
        TimeSpan claimLease,
        CancellationToken ct = default);

    Task MarkSentAsync(
        Guid messageId,
        DateTimeOffset now,
        CancellationToken ct = default);

    Task<OutboxMessageFailureInfo> MarkFailedAttemptAsync(
        Guid messageId,
        DateTimeOffset now,
        string error,
        CancellationToken ct = default);
}

public sealed record OutboxMessageFailureInfo(
    int AttemptCount,
    int MaxAttempts,
    ReplyInMyVoice.Domain.Enums.OutboxMessageStatus Status,
    DateTimeOffset NextAttemptAt);
