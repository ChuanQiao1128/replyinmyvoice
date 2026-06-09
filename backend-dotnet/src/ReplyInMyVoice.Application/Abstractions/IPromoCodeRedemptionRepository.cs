using ReplyInMyVoice.Domain.Entities;

namespace ReplyInMyVoice.Application.Abstractions;

public interface IPromoCodeRedemptionRepository
{
    Task AddAsync(PromoCodeRedemption redemption, CancellationToken ct = default);

    Task<bool> HasAppliedForUserAsync(Guid userId, CancellationToken ct = default);

    Task<bool> ExistsForPromoCodeAndUserAsync(
        Guid promoCodeId,
        Guid userId,
        CancellationToken ct = default);

    Task<int> CountAppliedByIpHashSinceAsync(
        string ipHash,
        DateTimeOffset since,
        CancellationToken ct = default);

    Task<IReadOnlyList<PromoCodeRedemption>> ListByUserIdAsync(
        Guid userId,
        CancellationToken ct = default);

    bool IsPromoCodeUserUniqueConstraintViolation(Exception exception);
}
