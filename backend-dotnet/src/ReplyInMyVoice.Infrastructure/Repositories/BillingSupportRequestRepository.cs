using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Infrastructure.Data;

namespace ReplyInMyVoice.Infrastructure.Repositories;

public sealed class BillingSupportRequestRepository(AppDbContext db) : IBillingSupportRequestRepository
{
    public async Task<IReadOnlyList<BillingSupportRequest>> ListByUserIdAsync(
        Guid userId,
        CancellationToken ct = default) =>
        await db.BillingSupportRequests
            .AsTracking()
            .Where(x => x.UserId == userId)
            .ToListAsync(ct);
}
