using ReplyInMyVoice.Domain.Entities;

namespace ReplyInMyVoice.Application.Abstractions;

public interface IBillingSupportRepository
{
    Task AddAsync(BillingSupportRequest request, CancellationToken ct = default);

    Task<bool> HasOpenRequestForUserAsync(
        Guid userId,
        CancellationToken ct = default);

    Task<bool> HasPaymentIntentForUserAsync(
        Guid userId,
        string paymentIntentId,
        CancellationToken ct = default);

    Task<IReadOnlyList<BillingSupportRequest>> ListByUserIdAsync(
        Guid userId,
        CancellationToken ct = default);
}
