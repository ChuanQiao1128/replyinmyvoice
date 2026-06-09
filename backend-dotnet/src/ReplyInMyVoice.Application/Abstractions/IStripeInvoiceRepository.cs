using ReplyInMyVoice.Domain.Entities;

namespace ReplyInMyVoice.Application.Abstractions;

public interface IStripeInvoiceRepository
{
    Task<IReadOnlyList<StripeInvoice>> ListByUserIdAsync(
        Guid userId,
        CancellationToken ct = default);
}
