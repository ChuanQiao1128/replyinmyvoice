using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Infrastructure.Data;

namespace ReplyInMyVoice.Infrastructure.Repositories;

public sealed class PromoCodeRepository(AppDbContext db) : IPromoCodeRepository
{
    public async Task<PromoCode?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await db.PromoCodes
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id, ct);

    public async Task<PromoCode?> GetByCodeAsync(string code, CancellationToken ct = default) =>
        await db.PromoCodes
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Code == code, ct);

    public async Task<IReadOnlyList<PromoCode>> ListAllAsync(CancellationToken ct = default) =>
        await db.PromoCodes
            .AsNoTracking()
            .ToListAsync(ct);

    public async Task<int> TryIncrementRedemptionCountAsync(
        Guid promoCodeId,
        DateTimeOffset now,
        CancellationToken ct = default)
    {
        var rowVersion = Guid.NewGuid();
        return await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE PromoCodes
            SET RedemptionCount = RedemptionCount + 1,
                UpdatedAt = {now},
                RowVersion = {rowVersion}
            WHERE Id = {promoCodeId}
              AND IsActive = 1
              AND {now} BETWEEN ValidFrom AND ValidUntil
              AND (MaxRedemptionsGlobal IS NULL OR RedemptionCount < MaxRedemptionsGlobal)
            """,
            ct);
    }
}
