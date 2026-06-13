using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Infrastructure.Data;

namespace ReplyInMyVoice.Infrastructure.Repositories;

public sealed class StripeReconciliationRunRepository(AppDbContext db) : IStripeReconciliationRunRepository
{
    public async Task AddAsync(
        StripeReconciliationRun run,
        CancellationToken ct = default)
    {
        await db.StripeReconciliationRuns.AddAsync(run, ct);
    }
}
