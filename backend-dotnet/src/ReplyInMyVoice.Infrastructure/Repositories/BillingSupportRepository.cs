using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Infrastructure.Data;

namespace ReplyInMyVoice.Infrastructure.Repositories;

public sealed class BillingSupportRepository(AppDbContext db) : IBillingSupportRepository
{
    public async Task AddAsync(BillingSupportRequest request, CancellationToken ct = default)
    {
        await db.BillingSupportRequests.AddAsync(request, ct);
    }

    public async Task<bool> HasOpenRequestForUserAsync(
        Guid userId,
        CancellationToken ct = default) =>
        await db.BillingSupportRequests
            .AsNoTracking()
            .AnyAsync(
                x => x.UserId == userId &&
                    x.Status == BillingSupportRequestStatus.Open,
                ct);

    public async Task<bool> HasPaymentIntentForUserAsync(
        Guid userId,
        string paymentIntentId,
        CancellationToken ct = default) =>
        await db.RewriteCredits
            .AsNoTracking()
            .AnyAsync(
                x => x.UserId == userId &&
                    x.StripePaymentIntentId == paymentIntentId,
                ct);

    public async Task<IReadOnlyList<BillingSupportRequest>> ListByUserIdAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var rows = await db.BillingSupportRequests
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .ToListAsync(ct);

        return rows
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .ToList();
    }
}
