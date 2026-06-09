using ReplyInMyVoice.Domain.Entities;

namespace ReplyInMyVoice.Application.Abstractions;

public interface IPromoCodeRepository
{
    Task<IReadOnlyList<PromoCode>> ListAllAsync(CancellationToken ct = default);
}
