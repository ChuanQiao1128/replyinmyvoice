using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Infrastructure.Data;

namespace ReplyInMyVoice.Infrastructure.Repositories;

public sealed class UsagePeriodRepository(AppDbContext db) : IUsagePeriodRepository
{
    public async Task AddAsync(UsagePeriod usagePeriod, CancellationToken ct = default)
    {
        await db.UsagePeriods.AddAsync(usagePeriod, ct);
    }

    public async Task<UsagePeriod?> GetByUserIdAndPeriodKeyAsync(
        Guid userId,
        string periodKey,
        CancellationToken ct = default) =>
        await db.UsagePeriods
            .AsTracking()
            .SingleOrDefaultAsync(
                x => x.UserId == userId && x.PeriodKey == periodKey,
                ct);
}
