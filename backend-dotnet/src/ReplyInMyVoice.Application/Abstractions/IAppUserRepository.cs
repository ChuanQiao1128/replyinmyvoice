using ReplyInMyVoice.Domain.Entities;

namespace ReplyInMyVoice.Application.Abstractions;

public interface IAppUserRepository
{
    Task AddAsync(AppUser user, CancellationToken ct = default);

    Task<AppUser?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<AppUser?> GetByExternalAuthUserIdAsync(string externalAuthUserId, CancellationToken ct = default);

    Task<AppUser?> GetByStripeCustomerIdAsync(string stripeCustomerId, CancellationToken ct = default);

    Task<AppUser?> FindForStripeCheckoutAsync(
        string? externalAuthUserId,
        string? stripeCustomerId,
        CancellationToken ct = default);

    Task<AppUser?> FindByStripeCustomerOrSubscriptionAsync(
        string? stripeCustomerId,
        string? stripeSubscriptionId,
        CancellationToken ct = default);

    Task<IReadOnlyList<AppUser>> ListExpiredPaymentGraceBatchAsync(
        DateTimeOffset now,
        int batchSize,
        CancellationToken ct = default);

    Task<IReadOnlyList<AppUser>> ListPaymentGraceReminderCandidatesBatchAsync(
        DateTimeOffset now,
        int batchSize,
        CancellationToken ct = default);
}
