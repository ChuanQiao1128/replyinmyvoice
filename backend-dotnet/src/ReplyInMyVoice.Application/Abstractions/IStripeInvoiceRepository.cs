using ReplyInMyVoice.Domain.Entities;

namespace ReplyInMyVoice.Application.Abstractions;

public interface IStripeInvoiceRepository
{
    Task AddAsync(StripeInvoice invoice, CancellationToken ct = default);

    Task<StripeInvoice?> GetByIdAsync(
        string invoiceId,
        CancellationToken ct = default);

    Task<IReadOnlyList<StripeInvoice>> ListByUserIdAsync(
        Guid userId,
        CancellationToken ct = default);
}
