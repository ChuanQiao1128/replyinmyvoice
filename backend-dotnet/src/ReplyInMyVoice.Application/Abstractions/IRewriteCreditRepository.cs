using ReplyInMyVoice.Domain.Entities;

namespace ReplyInMyVoice.Application.Abstractions;

public interface IRewriteCreditRepository
{
    Task AddAsync(RewriteCredit credit, CancellationToken ct = default);

    Task<RewriteCredit?> GetByIdAsync(Guid creditId, CancellationToken ct = default);

    Task<RewriteCredit?> GetUsableForReservationAsync(
        Guid userId,
        DateTimeOffset now,
        CancellationToken ct = default);

    Task<IReadOnlyList<RewriteCredit>> ListByUserIdAsync(
        Guid userId,
        CancellationToken ct = default);
}
