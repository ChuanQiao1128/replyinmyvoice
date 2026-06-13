using ReplyInMyVoice.Domain.Entities;

namespace ReplyInMyVoice.Application.Abstractions;

public interface IStripeEventRepository
{
    Task AddAsync(StripeEvent stripeEvent, CancellationToken ct = default);

    Task<StripeEvent?> GetByEventIdAsync(
        string eventId,
        CancellationToken ct = default);

    Task<IReadOnlyList<StripeEvent>> ClaimDueAsync(
        DateTimeOffset now,
        int batchSize,
        TimeSpan lease,
        string? eventId,
        CancellationToken ct = default);

    void MarkProcessed(StripeEvent stripeEvent, DateTimeOffset now);

    void MarkRetryScheduled(
        StripeEvent stripeEvent,
        string error,
        DateTimeOffset nextAttemptAt,
        DateTimeOffset now);

    void MarkFailed(StripeEvent stripeEvent, string error, DateTimeOffset now);

    bool IsDuplicateEventWriteFailure(Exception exception);
}
