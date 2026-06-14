using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
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

    public async Task<IReadOnlyList<BillingSupportRequest>> ListOpenForAdminQueueAsync(
        CancellationToken ct = default)
    {
        var rows = await db.BillingSupportRequests
            .AsNoTracking()
            .Include(x => x.User)
            .Where(x => x.Status == BillingSupportRequestStatus.Open)
            .ToListAsync(ct);

        return rows
            .OrderBy(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .ToList();
    }

    public async Task<BillingSupportRequest?> GetByIdForAdminResolveAsync(
        Guid requestId,
        CancellationToken ct = default) =>
        await db.BillingSupportRequests
            .Include(x => x.User)
            .AsTracking()
            .SingleOrDefaultAsync(x => x.Id == requestId, ct);

    public Task MarkResolvedAsync(
        BillingSupportRequest request,
        DateTimeOffset now,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        request.Status = BillingSupportRequestStatus.Resolved;
        request.ResolvedAt = now;
        request.UpdatedAt = now;
        return Task.CompletedTask;
    }
}
