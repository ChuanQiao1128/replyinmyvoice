using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.Common;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Infrastructure.Data;

namespace ReplyInMyVoice.Infrastructure.Repositories;

public sealed class PromoAdminRepository(AppDbContext db) : IPromoAdminRepository
{
    public async Task<IReadOnlyList<PromoCode>> ListPromoCodesAsync(CancellationToken ct = default) =>
        await db.PromoCodes
            .AsNoTracking()
            .ToListAsync(ct);

    public async Task<PromoCode?> GetPromoCodeByIdAsync(
        Guid promoCodeId,
        CancellationToken ct = default) =>
        await db.PromoCodes
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == promoCodeId, ct);

    public async Task<PromoCode?> GetPromoCodeByIdForUpdateAsync(
        Guid promoCodeId,
        CancellationToken ct = default) =>
        await db.PromoCodes
            .AsTracking()
            .SingleOrDefaultAsync(x => x.Id == promoCodeId, ct);

    public async Task<bool> CodeExistsAsync(
        string normalizedCode,
        CancellationToken ct = default) =>
        await db.PromoCodes
            .AsNoTracking()
            .AnyAsync(x => x.Code == normalizedCode, ct);

    public async Task AddPromoCodeAsync(
        PromoCode promoCode,
        CancellationToken ct = default)
    {
        await db.PromoCodes.AddAsync(promoCode, ct);
    }

    public async Task AddAuditLogAsync(
        AdminAuditLog auditLog,
        CancellationToken ct = default)
    {
        await db.AdminAuditLogs.AddAsync(auditLog, ct);
    }

    public async Task<IReadOnlyList<AdminPromoRedemptionRowDto>> ListAppliedRedemptionsAsync(
        Guid promoCodeId,
        CancellationToken ct = default) =>
        await db.PromoCodeRedemptions
            .AsNoTracking()
            .Where(x => x.PromoCodeId == promoCodeId && x.Status == PromoCodeRedemptionStatus.Applied)
            .Select(x => new AdminPromoRedemptionRowDto(
                x.UserId,
                x.RewriteCreditId,
                x.RedeemIpHash,
                x.RedeemedAt))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Guid>> ListActivatedCreditIdsAsync(
        IReadOnlyCollection<Guid> rewriteCreditIds,
        CancellationToken ct = default)
    {
        if (rewriteCreditIds.Count == 0)
        {
            return [];
        }

        return await db.RewriteCredits
            .AsNoTracking()
            .Where(x => rewriteCreditIds.Contains(x.Id) && x.AmountConsumed > 0)
            .Select(x => x.Id)
            .ToListAsync(ct);
    }

    public bool IsPromoCodeUniqueConstraintViolation(Exception exception)
    {
        var message = exception.ToString();
        return message.Contains("IX_PromoCodes_Code", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("PromoCodes.Code", StringComparison.OrdinalIgnoreCase);
    }
}
