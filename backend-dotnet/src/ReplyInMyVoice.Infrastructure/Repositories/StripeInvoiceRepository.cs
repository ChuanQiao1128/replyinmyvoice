using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Infrastructure.Data;

namespace ReplyInMyVoice.Infrastructure.Repositories;

public sealed class StripeInvoiceRepository(AppDbContext db) : IStripeInvoiceRepository
{
    public async Task<IReadOnlyList<StripeInvoice>> ListByUserIdAsync(
        Guid userId,
        CancellationToken ct = default) =>
        await db.StripeInvoices
            .AsTracking()
            .Where(x => x.UserId == userId)
            .ToListAsync(ct);
}
