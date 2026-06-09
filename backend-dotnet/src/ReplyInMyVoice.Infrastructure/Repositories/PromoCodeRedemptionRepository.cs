using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Infrastructure.Data;

namespace ReplyInMyVoice.Infrastructure.Repositories;

public sealed class PromoCodeRedemptionRepository(AppDbContext db) : IPromoCodeRedemptionRepository
{
    public async Task AddAsync(PromoCodeRedemption redemption, CancellationToken ct = default)
    {
        await db.PromoCodeRedemptions.AddAsync(redemption, ct);
    }

    public async Task<bool> HasAppliedForUserAsync(Guid userId, CancellationToken ct = default) =>
        await db.PromoCodeRedemptions
            .AsNoTracking()
            .AnyAsync(
                x => x.UserId == userId && x.Status == PromoCodeRedemptionStatus.Applied,
                ct);

    public async Task<bool> ExistsForPromoCodeAndUserAsync(
        Guid promoCodeId,
        Guid userId,
        CancellationToken ct = default) =>
        await db.PromoCodeRedemptions
            .AsNoTracking()
            .AnyAsync(
                x => x.PromoCodeId == promoCodeId && x.UserId == userId,
                ct);

    public async Task<int> CountAppliedByIpHashSinceAsync(
        string ipHash,
        DateTimeOffset since,
        CancellationToken ct = default)
    {
        var redeemedAtValues = await db.PromoCodeRedemptions
            .AsNoTracking()
            .Where(x => x.RedeemIpHash == ipHash && x.Status == PromoCodeRedemptionStatus.Applied)
            .Select(x => x.RedeemedAt)
            .ToListAsync(ct);

        return redeemedAtValues.Count(x => x > since);
    }

    public async Task<IReadOnlyList<PromoCodeRedemption>> ListByUserIdAsync(
        Guid userId,
        CancellationToken ct = default) =>
        await db.PromoCodeRedemptions
            .AsTracking()
            .Where(x => x.UserId == userId)
            .ToListAsync(ct);

    public bool IsPromoCodeUserUniqueConstraintViolation(Exception exception)
    {
        var message = exception.ToString();
        return message.Contains("IX_PromoCodeRedemptions_PromoCodeId_UserId", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("PromoCodeRedemptions.PromoCodeId", StringComparison.OrdinalIgnoreCase) &&
            message.Contains("PromoCodeRedemptions.UserId", StringComparison.OrdinalIgnoreCase);
    }
}
