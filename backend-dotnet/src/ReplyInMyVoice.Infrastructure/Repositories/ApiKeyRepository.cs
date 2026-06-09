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
}
