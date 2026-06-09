using ReplyInMyVoice.Domain.Entities;

namespace ReplyInMyVoice.Application.Abstractions;

public interface IPromoCodeRepository
{
    Task<PromoCode?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<PromoCode?> GetByCodeAsync(string code, CancellationToken ct = default);

    Task<IReadOnlyList<PromoCode>> ListAllAsync(CancellationToken ct = default);

    Task<int> TryIncrementRedemptionCountAsync(
        Guid promoCodeId,
        DateTimeOffset now,
        CancellationToken ct = default);
}
