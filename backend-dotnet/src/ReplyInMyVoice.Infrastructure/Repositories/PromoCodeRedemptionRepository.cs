using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Infrastructure.Data;

namespace ReplyInMyVoice.Infrastructure.Repositories;

public sealed class PromoCodeRedemptionRepository(AppDbContext db) : IPromoCodeRedemptionRepository
{
    public async Task<bool> HasAppliedForUserAsync(Guid userId, CancellationToken ct = default) =>
        await db.PromoCodeRedemptions
            .AsTracking()
            .AnyAsync(
                x => x.UserId == userId && x.Status == PromoCodeRedemptionStatus.Applied,
                ct);

    public async Task<IReadOnlyList<PromoCodeRedemption>> ListByUserIdAsync(
        Guid userId,
        CancellationToken ct = default) =>
        await db.PromoCodeRedemptions
            .AsTracking()
            .Where(x => x.UserId == userId)
            .ToListAsync(ct);
}
