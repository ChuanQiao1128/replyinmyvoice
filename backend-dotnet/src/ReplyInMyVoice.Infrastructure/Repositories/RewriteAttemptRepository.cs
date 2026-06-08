using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Infrastructure.Data;

namespace ReplyInMyVoice.Infrastructure.Repositories;

public sealed class RewriteAttemptRepository(AppDbContext db) : IRewriteAttemptRepository
{
    public async Task AddAsync(RewriteAttempt attempt, CancellationToken ct = default)
    {
        await db.RewriteAttempts.AddAsync(attempt, ct);
    }

    public async Task<RewriteAttempt?> GetByIdForUserAsync(
        Guid attemptId,
        Guid userId,
        CancellationToken ct = default) =>
        await db.RewriteAttempts
            .AsTracking()
            .SingleOrDefaultAsync(
                x => x.Id == attemptId && x.UserId == userId && x.DeletedAt == null,
                ct);

    public async Task<RewriteAttempt?> GetByUserIdAndIdempotencyKeyAsync(
        Guid userId,
        string idempotencyKey,
        CancellationToken ct = default) =>
        await db.RewriteAttempts
            .AsTracking()
            .SingleOrDefaultAsync(
                x => x.UserId == userId && x.IdempotencyKey == idempotencyKey,
                ct);
}
