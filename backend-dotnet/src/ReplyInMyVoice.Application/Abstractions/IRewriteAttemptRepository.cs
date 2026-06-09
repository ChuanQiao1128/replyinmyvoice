using ReplyInMyVoice.Domain.Entities;

namespace ReplyInMyVoice.Application.Abstractions;

public interface IRewriteAttemptRepository
{
    Task AddAsync(RewriteAttempt attempt, CancellationToken ct = default);

    Task<RewriteAttempt?> GetByIdAsync(Guid attemptId, CancellationToken ct = default);

    Task<RewriteAttempt?> GetByIdNoTrackingAsync(Guid attemptId, CancellationToken ct = default);

    Task<RewriteAttempt?> GetByIdForUserAsync(Guid attemptId, Guid userId, CancellationToken ct = default);

    Task<RewriteAttempt?> GetByUserIdAndIdempotencyKeyAsync(
        Guid userId,
        string idempotencyKey,
        CancellationToken ct = default);

    Task<IReadOnlyList<RewriteAttempt>> ListByUserIdAsync(
        Guid userId,
        CancellationToken ct = default);
}
