using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Infrastructure.Data;

namespace ReplyInMyVoice.Infrastructure.Repositories;

public sealed class AppUserRepository(AppDbContext db) : IAppUserRepository
{
    public async Task<AppUser?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await db.AppUsers
            .AsTracking()
            .SingleOrDefaultAsync(x => x.Id == id, ct);

    public async Task<AppUser?> GetByExternalAuthUserIdAsync(string externalAuthUserId, CancellationToken ct = default) =>
        await db.AppUsers
            .AsTracking()
            .SingleOrDefaultAsync(x => x.ExternalAuthUserId == externalAuthUserId, ct);
}
