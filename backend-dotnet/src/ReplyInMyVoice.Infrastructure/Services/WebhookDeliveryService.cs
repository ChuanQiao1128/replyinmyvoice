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
        var attempt = await db.RewriteAttempts
            .AsNoTracking()
            .Include(x => x.ApiKey)
            .SingleOrDefaultAsync(x => x.Id == attemptId, cancellationToken);

        if (attempt?.ApiKeyId is not { } apiKeyId ||
            attempt.ApiKey is null ||
            string.IsNullOrWhiteSpace(attempt.ApiKey.WebhookUrl) ||
            string.IsNullOrWhiteSpace(attempt.ApiKey.WebhookSecret))
        {
            return;
        }

        var exists = await db.WebhookDeliveries
            .AsNoTracking()
            .AnyAsync(
                x => x.ApiKeyId == apiKeyId && x.RewriteAttemptId == attemptId,
                cancellationToken);
        if (exists)
        {
            return;
        }

        db.WebhookDeliveries.Add(new WebhookDelivery
        {
            ApiKeyId = apiKeyId,
            RewriteAttemptId = attemptId,
            Url = attempt.ApiKey.WebhookUrl,
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
                apiKeyId);
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
