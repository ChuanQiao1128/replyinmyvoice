using ReplyInMyVoice.Domain.Entities;

namespace ReplyInMyVoice.Application.Abstractions;

public interface IStripeEventRepository
{
    Task AddAsync(StripeEvent stripeEvent, CancellationToken ct = default);

    Task<StripeEvent?> GetByEventIdAsync(
        string eventId,
        CancellationToken ct = default);

    Task<StripeEvent?> BeginProcessingAsync(
        string eventId,
        string type,
        DateTimeOffset now,
        CancellationToken ct = default);

    void MarkProcessed(StripeEvent stripeEvent, DateTimeOffset now);

    void MarkFailed(StripeEvent stripeEvent, string error, DateTimeOffset now);

    bool IsDuplicateEventWriteFailure(Exception exception);
}
