using ReplyInMyVoice.Domain.Entities;

namespace ReplyInMyVoice.Application.Abstractions;

public interface IBillingSupportRequestRepository
{
    Task<IReadOnlyList<BillingSupportRequest>> ListByUserIdAsync(
        Guid userId,
        CancellationToken ct = default);
}
