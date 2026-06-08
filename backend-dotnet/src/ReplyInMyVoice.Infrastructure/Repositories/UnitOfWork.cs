using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Infrastructure.Data;

namespace ReplyInMyVoice.Infrastructure.Repositories;

public sealed class UnitOfWork(AppDbContext db) : IUnitOfWork
{
    public async Task<int> SaveChangesAsync(CancellationToken ct = default) =>
        await db.SaveChangesAsync(ct);
}
