using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Infrastructure.Data;

namespace ReplyInMyVoice.Infrastructure.Services;

public interface IWebhookDeliveryEnqueuer
{
    Task EnqueueForTerminalAttemptAsync(
        Guid attemptId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);
}

public sealed class WebhookDeliveryService(
    Func<AppDbContext> dbContextFactory,
    ILogger<WebhookDeliveryService>? logger = null) : IWebhookDeliveryEnqueuer
{
    public async Task EnqueueForTerminalAttemptAsync(
        Guid attemptId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        await using var db = dbContextFactory();
        var requestId = attemptId.ToString();
        var query = db.ApiKeyUsages
            .AsNoTracking()
            .Include(x => x.ApiKey)
            .Where(x => x.RequestId == requestId);

        var usage = db.Database.IsSqlite()
            ? (await query.ToListAsync(cancellationToken))
                .OrderBy(x => x.CreatedAt)
                .FirstOrDefault()
            : await query
                .OrderBy(x => x.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

        if (usage?.ApiKey is null ||
            string.IsNullOrWhiteSpace(usage.ApiKey.WebhookUrl) ||
            string.IsNullOrWhiteSpace(usage.ApiKey.WebhookSecret))
        {
            return;
        }

        var exists = await db.WebhookDeliveries
            .AsNoTracking()
            .AnyAsync(
                x => x.ApiKeyId == usage.ApiKeyId && x.RewriteAttemptId == attemptId,
                cancellationToken);
        if (exists)
        {
            return;
        }

        db.WebhookDeliveries.Add(new WebhookDelivery
        {
            ApiKeyId = usage.ApiKeyId,
            RewriteAttemptId = attemptId,
            Url = usage.ApiKey.WebhookUrl,
            Status = WebhookDeliveryStatus.Pending,
            AttemptCount = 0,
            MaxAttempts = 5,
            CreatedAt = now,
            NextAttemptAt = now,
        });

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsDuplicateDelivery(ex))
        {
            logger?.LogInformation(
                "Webhook delivery already exists for attempt {AttemptId} and API key {ApiKeyId}.",
                attemptId,
                usage.ApiKeyId);
        }
    }

    private static bool IsDuplicateDelivery(DbUpdateException exception)
    {
        var message = exception.ToString();
        return message.Contains("IX_WebhookDeliveries_ApiKeyId_RewriteAttemptId", StringComparison.OrdinalIgnoreCase) ||
            (message.Contains("WebhookDeliveries.ApiKeyId", StringComparison.OrdinalIgnoreCase) &&
                message.Contains("WebhookDeliveries.RewriteAttemptId", StringComparison.OrdinalIgnoreCase));
    }
}
