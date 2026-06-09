using ReplyInMyVoice.Application.Common;
using ReplyInMyVoice.Domain.Entities;

namespace ReplyInMyVoice.Application.Abstractions;

public interface IPromoAdminRepository
{
    Task<IReadOnlyList<PromoCode>> ListPromoCodesAsync(CancellationToken ct = default);

    Task<PromoCode?> GetPromoCodeByIdAsync(
        Guid promoCodeId,
        CancellationToken ct = default);

    Task<PromoCode?> GetPromoCodeByIdForUpdateAsync(
        Guid promoCodeId,
        CancellationToken ct = default);

    Task<bool> CodeExistsAsync(
        string normalizedCode,
        CancellationToken ct = default);

    Task AddPromoCodeAsync(
        PromoCode promoCode,
        CancellationToken ct = default);

    Task AddAuditLogAsync(
        AdminAuditLog auditLog,
        CancellationToken ct = default);

    Task<IReadOnlyList<AdminPromoRedemptionRowDto>> ListAppliedRedemptionsAsync(
        Guid promoCodeId,
        CancellationToken ct = default);

    Task<IReadOnlyList<Guid>> ListActivatedCreditIdsAsync(
        IReadOnlyCollection<Guid> rewriteCreditIds,
        CancellationToken ct = default);

    bool IsPromoCodeUniqueConstraintViolation(Exception exception);
}
