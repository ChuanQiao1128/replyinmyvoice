using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ReplyInMyVoice.Domain.Contracts;
using ReplyInMyVoice.Infrastructure.Data;
using ReplyInMyVoice.Infrastructure.Queueing;

namespace ReplyInMyVoice.Infrastructure.Services;

public sealed class RewriteRequestService(
    Func<AppDbContext> dbContextFactory,
    QuotaService quotaService,
    IRewriteJobPublisher jobPublisher)
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
        // Keep the factory dependency here so tests prove this service is wired to the same DB boundary.
        _ = dbContextFactory;

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

        if (result.Kind == ReserveRewriteResultKind.Created)
        {
            await jobPublisher.PublishAsync(new RewriteJob(result.AttemptId), cancellationToken);
        }

        return result;
    }

    private static string ComputeRequestHash(RewriteRequest request)
    {
        var json = JsonSerializer.Serialize(request);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
