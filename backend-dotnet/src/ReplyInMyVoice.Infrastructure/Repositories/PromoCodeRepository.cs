using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Infrastructure.Data;

namespace ReplyInMyVoice.Infrastructure.Repositories;

public sealed class PromoCodeRepository(AppDbContext db) : IPromoCodeRepository
{
    public async Task<IReadOnlyList<PromoCode>> ListAllAsync(CancellationToken ct = default) =>
        await db.PromoCodes
            .AsTracking()
            .ToListAsync(ct);
}
