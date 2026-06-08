using ReplyInMyVoice.Domain.Entities;

namespace ReplyInMyVoice.Application.Abstractions;

public interface IRewriteCreditRepository
{
    Task<RewriteCredit?> GetUsableForReservationAsync(
        Guid userId,
        DateTimeOffset now,
        CancellationToken ct = default);
}
