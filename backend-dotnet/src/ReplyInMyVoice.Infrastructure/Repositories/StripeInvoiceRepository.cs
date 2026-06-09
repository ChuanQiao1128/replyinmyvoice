using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Infrastructure.Data;

namespace ReplyInMyVoice.Infrastructure.Repositories;

public sealed class StripeInvoiceRepository(AppDbContext db) : IStripeInvoiceRepository
{
    public async Task AddAsync(StripeInvoice invoice, CancellationToken ct = default)
    {
        await db.StripeInvoices.AddAsync(invoice, ct);
    }

    public async Task<StripeInvoice?> GetByIdAsync(
        string invoiceId,
        CancellationToken ct = default) =>
        await db.StripeInvoices
            .AsTracking()
            .SingleOrDefaultAsync(x => x.Id == invoiceId, ct);

    public async Task<IReadOnlyList<StripeInvoice>> ListByUserIdAsync(
        Guid userId,
        CancellationToken ct = default) =>
        await db.StripeInvoices
            .AsTracking()
            .Where(x => x.UserId == userId)
            .ToListAsync(ct);
}
