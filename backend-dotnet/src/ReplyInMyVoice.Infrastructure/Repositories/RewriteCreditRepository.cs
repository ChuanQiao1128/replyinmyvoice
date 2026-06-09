using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Infrastructure.Data;

namespace ReplyInMyVoice.Infrastructure.Repositories;

public sealed class RewriteCreditRepository(AppDbContext db) : IRewriteCreditRepository
{
    public async Task AddAsync(RewriteCredit credit, CancellationToken ct = default)
    {
        await db.RewriteCredits.AddAsync(credit, ct);
    }

    public async Task<RewriteCredit?> GetByIdAsync(
        Guid creditId,
        CancellationToken ct = default) =>
        await db.RewriteCredits
            .AsTracking()
            .SingleOrDefaultAsync(x => x.Id == creditId, ct);

    public async Task<RewriteCredit?> GetUsableForReservationAsync(
        Guid userId,
        DateTimeOffset now,
        CancellationToken ct = default)
    {
        var userCredits = await db.RewriteCredits
            .AsTracking()
            .Where(x => x.UserId == userId)
            .ToListAsync(ct);

        return userCredits
            .Where(x => (x.ExpiresAt == null || x.ExpiresAt > now) && x.AmountGranted - x.AmountConsumed > 0)
            .OrderBy(x => x.ExpiresAt.HasValue ? 0 : 1)
            .ThenBy(x => x.ExpiresAt ?? DateTimeOffset.MaxValue)
            .ThenBy(x => x.GrantedAt)
            .FirstOrDefault();
    }

    public async Task<IReadOnlyList<RewriteCredit>> ListByUserIdAsync(
        Guid userId,
        CancellationToken ct = default) =>
        await db.RewriteCredits
            .AsTracking()
            .Where(x => x.UserId == userId)
            .ToListAsync(ct);

    public async Task<RewriteCredit?> GetByUserIdAndPaymentIntentIdAsync(
        Guid userId,
        string paymentIntentId,
        CancellationToken ct = default) =>
        await db.RewriteCredits
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.UserId == userId && x.StripePaymentIntentId == paymentIntentId,
                ct);

    public async Task<IReadOnlyList<RewriteCredit>> ListPurchaseCreditsForTurnoverAsync(
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        CancellationToken ct = default)
    {
        var rows = await db.RewriteCredits
            .AsNoTracking()
            .Where(x =>
                x.Source == "PURCHASE" &&
                x.StripeAmountTotal.HasValue)
            .ToListAsync(ct);

        return rows
            .Where(x =>
                x.StripeAmountTotal is > 0 &&
                x.GrantedAt >= windowStart &&
                x.GrantedAt <= windowEnd)
            .ToList();
    }
}
