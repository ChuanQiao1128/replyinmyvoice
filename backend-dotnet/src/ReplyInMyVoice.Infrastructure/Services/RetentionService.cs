using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Infrastructure.Data;

namespace ReplyInMyVoice.Infrastructure.Services;

public sealed class RetentionService(Func<AppDbContext> dbContextFactory)
{
    public const int DefaultRetentionDays = 90;

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
            .AsTracking()
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
}
