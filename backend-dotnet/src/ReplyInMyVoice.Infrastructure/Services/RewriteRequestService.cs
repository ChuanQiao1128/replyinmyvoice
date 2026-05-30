using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Domain.Contracts;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Infrastructure.Data;

namespace ReplyInMyVoice.Infrastructure.Services;

public sealed class RewriteRequestService(
    Func<AppDbContext> dbContextFactory,
    QuotaService quotaService)
{
    public async Task<ReserveRewriteResult> CreateAttemptAsync(
        Guid userId,
        string idempotencyKey,
        RewriteRequest request,
        string periodKey,
        int quotaLimit,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using (var db = dbContextFactory())
        {
            var suspended = await db.AppUsers
                .AsNoTracking()
                .Where(x => x.Id == userId)
                .Select(x => x.SuspendedAt != null)
                .SingleOrDefaultAsync(cancellationToken);

            if (suspended)
            {
                return new ReserveRewriteResult(
                    ReserveRewriteResultKind.QuotaExceeded,
                    Guid.Empty,
                    RewriteAttemptStatus.Failed,
                    ErrorCode: "user_suspended");
            }
        }

        var result = await quotaService.ReserveAsync(
            userId,
            idempotencyKey,
            ComputeRequestHash(request),
            JsonSerializer.Serialize(request),
            periodKey,
            quotaLimit,
            now,
            TimeSpan.FromMinutes(15),
            cancellationToken);

        return result;
    }

    private static string ComputeRequestHash(RewriteRequest request)
    {
        var json = JsonSerializer.Serialize(request);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
