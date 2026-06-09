using ReplyInMyVoice.Domain.Entities;

namespace ReplyInMyVoice.Application.Abstractions;

public interface IBillingSupportRequestRepository
{
    Task<IReadOnlyList<BillingSupportRequest>> ListByUserIdAsync(
        Guid userId,
        CancellationToken ct = default);

    Task<IReadOnlyList<BillingSupportRequest>> ListOpenForAdminQueueAsync(
        CancellationToken ct = default);

    Task<BillingSupportRequest?> GetByIdForAdminResolveAsync(
        Guid requestId,
        CancellationToken ct = default);

    Task MarkResolvedAsync(
        BillingSupportRequest request,
        DateTimeOffset now,
        CancellationToken ct = default);
}
