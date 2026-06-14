using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Infrastructure.Data;

namespace ReplyInMyVoice.Infrastructure.Repositories;

public sealed class ApiKeyRepository(AppDbContext db) : IApiKeyRepository
{
    public async Task AddAsync(ApiKey apiKey, CancellationToken ct = default)
    {
        await db.ApiKeys.AddAsync(apiKey, ct);
    }

    public async Task<ApiKey?> GetByKeyHashAsync(
        string keyHash,
        CancellationToken ct = default) =>
        await db.ApiKeys
            .AsTracking()
            .SingleOrDefaultAsync(x => x.KeyHash == keyHash, ct);

    public async Task<ApiKey?> GetByIdForUserAsync(
        Guid userId,
        Guid keyId,
        CancellationToken ct = default) =>
        await db.ApiKeys
            .AsTracking()
            .SingleOrDefaultAsync(
                x => x.Id == keyId && x.UserId == userId,
                ct);

    public async Task<IReadOnlyList<ApiKey>> ListByUserIdAsync(
        Guid userId,
        CancellationToken ct = default) =>
        await db.ApiKeys
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .ToListAsync(ct);

    public void TouchLastUsed(ApiKey apiKey, DateTimeOffset now)
    {
        apiKey.LastUsedAt = now;
    }

    public void DiscardPendingChanges(ApiKey apiKey)
    {
        db.Entry(apiKey).State = EntityState.Unchanged;
    }
}
