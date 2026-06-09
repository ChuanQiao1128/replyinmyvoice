using ReplyInMyVoice.Domain.Entities;

namespace ReplyInMyVoice.Application.Abstractions;

public interface IPromoCodeRedemptionRepository
{
    Task<bool> HasAppliedForUserAsync(Guid userId, CancellationToken ct = default);

    Task<IReadOnlyList<PromoCodeRedemption>> ListByUserIdAsync(
        Guid userId,
        CancellationToken ct = default);
}
