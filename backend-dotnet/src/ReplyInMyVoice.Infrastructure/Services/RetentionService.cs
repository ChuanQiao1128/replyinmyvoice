using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Domain.Contracts;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Infrastructure.Data;

namespace ReplyInMyVoice.Infrastructure.Services;

public sealed class RetentionService(Func<AppDbContext> dbContextFactory)
{
    public const int DefaultRetentionDays = 30;
    public const int DefaultSandboxRetentionDays = 7;

    public async Task<int> ScrubExpiredRawContentAsync(
        DateTimeOffset now,
        int retentionDays = DefaultRetentionDays,
        CancellationToken cancellationToken = default)
    {
        if (retentionDays <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(retentionDays), "Retention days must be greater than zero.");
        }

        await using var db = dbContextFactory();
        var cutoff = now.AddDays(-retentionDays);
        var rawContentQuery = db.RewriteAttempts
            .IgnoreQueryFilters()
            .AsTracking()
            .Where(x =>
                x.Status == RewriteAttemptStatus.Succeeded ||
                x.Status == RewriteAttemptStatus.Failed ||
                x.Status == RewriteAttemptStatus.Expired)
            .Where(x => x.RequestJson != null || x.ResultJson != null);
        var attempts = db.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite"
            ? (await rawContentQuery.ToListAsync(cancellationToken))
                .Where(x => x.CreatedAt < cutoff)
                .ToList()
            : await rawContentQuery
                .Where(x => x.CreatedAt < cutoff)
                .ToListAsync(cancellationToken);

        foreach (var attempt in attempts)
        {
            attempt.RequestJson = null!;
            attempt.ResultJson = null;
            attempt.RowVersion = Guid.NewGuid();
        }

        await db.SaveChangesAsync(cancellationToken);
        return attempts.Count;
    }

    public async Task<int> PurgeExpiredSandboxAttemptsAsync(
        DateTimeOffset now,
        int sandboxRetentionDays = DefaultSandboxRetentionDays,
        CancellationToken cancellationToken = default)
    {
        if (sandboxRetentionDays <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sandboxRetentionDays),
                "Sandbox retention days must be greater than zero.");
        }

        await using var db = dbContextFactory();
        var cutoff = now.AddDays(-sandboxRetentionDays);
        var sandboxQuery = db.RewriteAttempts
            .IgnoreQueryFilters()
            .AsTracking()
            .Where(x => x.ApiKeyId != null && db.ApiKeys.Any(k => k.Id == x.ApiKeyId && k.IsTest))
            .Where(x => x.IdempotencyKey.StartsWith(SandboxAttemptConventions.IdempotencyKeyPrefix))
            .Where(x => x.Reservation == null)
            .Where(x => !db.WebhookDeliveries.Any(d => d.RewriteAttemptId == x.Id));
        var attempts = db.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite"
            ? (await sandboxQuery.ToListAsync(cancellationToken))
                .Where(x => x.CreatedAt < cutoff)
                .ToList()
            : await sandboxQuery
                .Where(x => x.CreatedAt < cutoff)
                .ToListAsync(cancellationToken);

        db.RewriteAttempts.RemoveRange(attempts);
        await db.SaveChangesAsync(cancellationToken);
        return attempts.Count;
    }
}
