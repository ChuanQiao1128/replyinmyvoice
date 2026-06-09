using ReplyInMyVoice.Domain.Entities;

namespace ReplyInMyVoice.Application.Abstractions;

public interface IRewriteCreditRepository
{
    Task AddAsync(RewriteCredit credit, CancellationToken ct = default);

    Task<RewriteCredit?> GetByIdAsync(Guid creditId, CancellationToken ct = default);

    Task<RewriteCredit?> GetUsableForReservationAsync(
        Guid userId,
        DateTimeOffset now,
        CancellationToken ct = default);

    Task<IReadOnlyList<RewriteCredit>> ListByUserIdAsync(
        Guid userId,
        CancellationToken ct = default);

    Task<RewriteCredit?> GetByUserIdAndPaymentIntentIdAsync(
        Guid userId,
        string paymentIntentId,
        CancellationToken ct = default);

    Task<bool> ExistsByStripeEventIdAsync(
        string stripeEventId,
        CancellationToken ct = default);

    Task<IReadOnlyList<RewriteCredit>> ListByStripePaymentIntentIdAsync(
        string paymentIntentId,
        CancellationToken ct = default);

    Task<IReadOnlyList<RewriteCredit>> ListPurchaseCreditsForTurnoverAsync(
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        CancellationToken ct = default);

    Task<IReadOnlyList<RewriteCredit>> ListExpiryReminderCandidatesAsync(
        DateTimeOffset now,
        DateTimeOffset windowEnd,
        int batchSize,
        CancellationToken ct = default);

    Task MarkExpiryReminderSentAsync(
        RewriteCredit credit,
        DateTimeOffset sentAt,
        CancellationToken ct = default);

    bool IsStripeEventIdWriteFailure(Exception exception);
}
