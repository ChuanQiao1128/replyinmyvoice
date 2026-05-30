using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ReplyInMyVoice.Domain.Contracts;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Infrastructure.Data;
using ReplyInMyVoice.Infrastructure.Queueing;

namespace ReplyInMyVoice.Infrastructure.Services;

public sealed class OutboxDispatcherService(
    Func<AppDbContext> dbContextFactory,
    IRewriteJobPublisher jobPublisher,
    ILogger<OutboxDispatcherService>? logger = null)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async Task<int> DispatchDueAsync(
        DateTimeOffset now,
        string lockedBy,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        var messages = await ClaimDueMessagesAsync(now, lockedBy, batchSize, cancellationToken);
        foreach (var message in messages)
        {
            using var scope = BeginOutboxScope(message);
            var correlationId = ResolveCorrelationId(message);
            try
            {
                var job = CreateRewriteJob(message);
                await jobPublisher.PublishAsync(job, cancellationToken);
                await MarkSentAsync(message.Id, now, cancellationToken);
                logger?.LogInformation(
                    "Outbox message dispatched for attempt {AttemptId}. MessageId: {OutboxMessageId}. MessageType: {MessageType}.",
                    correlationId,
                    message.Id,
                    message.MessageType);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                var failure = await MarkFailedAttemptAsync(message.Id, now, ex.Message, cancellationToken);
                logger?.LogWarning(
                    ex,
                    "Outbox message dispatch failed for attempt {AttemptId}. MessageId: {OutboxMessageId}. MessageType: {MessageType}. AttemptCount: {AttemptCount}. MaxAttempts: {MaxAttempts}. Status: {OutboxStatus}.",
                    correlationId,
                    message.Id,
                    message.MessageType,
                    failure.AttemptCount,
                    failure.MaxAttempts,
                    failure.Status);
            }
        }

        return messages.Count;
    }

    private IDisposable? BeginOutboxScope(OutboxMessage message) =>
        logger?.BeginScope(new Dictionary<string, object>
        {
            ["attemptId"] = ResolveCorrelationId(message),
            ["outboxMessageId"] = message.Id,
        });

    private static string ResolveCorrelationId(OutboxMessage message) =>
        message.CorrelationId ?? message.Id.ToString();

    private async Task<List<OutboxMessage>> ClaimDueMessagesAsync(
        DateTimeOffset now,
        string lockedBy,
        int batchSize,
        CancellationToken cancellationToken)
    {
        return await ExecuteInTransactionAsync(async db =>
        {
            List<OutboxMessage> messages;
            if (db.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true)
            {
                var candidates = await db.OutboxMessages
                    .AsTracking()
                    .Where(x => x.Status == OutboxMessageStatus.Pending || x.Status == OutboxMessageStatus.Processing)
                    .ToListAsync(cancellationToken);

                messages = candidates
                    .Where(x => x.NextAttemptAt <= now && (x.LockedUntil is null || x.LockedUntil <= now))
                    .OrderBy(x => x.CreatedAt)
                    .Take(batchSize)
                    .ToList();
            }
            else
            {
                messages = await db.OutboxMessages
                    .AsTracking()
                    .Where(x => x.NextAttemptAt <= now)
                    .Where(x => x.LockedUntil == null || x.LockedUntil.Value <= now)
                    .Where(x => x.Status == OutboxMessageStatus.Pending || x.Status == OutboxMessageStatus.Processing)
                    .OrderBy(x => x.CreatedAt)
                    .Take(batchSize)
                    .ToListAsync(cancellationToken);
            }

            foreach (var message in messages)
            {
                message.Status = OutboxMessageStatus.Processing;
                message.LockedBy = lockedBy;
                message.LockedUntil = now.AddSeconds(30);
                message.LastAttemptAt = now;
                message.RowVersion = Guid.NewGuid();
            }

            await db.SaveChangesAsync(cancellationToken);
            return messages;
        }, cancellationToken);
    }

    private async Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<AppDbContext, Task<TResult>> operation,
        CancellationToken cancellationToken)
    {
        await using var strategyDb = dbContextFactory();
        var strategy = strategyDb.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            await using var db = dbContextFactory();
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

            var result = await operation(db);

            await transaction.CommitAsync(cancellationToken);
            return result;
        });
    }

    private async Task MarkSentAsync(
        Guid messageId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var db = dbContextFactory();
        var message = await db.OutboxMessages
            .AsTracking()
            .SingleAsync(x => x.Id == messageId, cancellationToken);

        message.Status = OutboxMessageStatus.Sent;
        message.SentAt = now;
        message.LastError = null;
        message.LockedBy = null;
        message.LockedUntil = null;
        message.RowVersion = Guid.NewGuid();

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<OutboxFailureLogInfo> MarkFailedAttemptAsync(
        Guid messageId,
        DateTimeOffset now,
        string error,
        CancellationToken cancellationToken)
    {
        await using var db = dbContextFactory();
        var message = await db.OutboxMessages
            .AsTracking()
            .SingleAsync(x => x.Id == messageId, cancellationToken);

        var nextAttemptCount = message.AttemptCount + 1;
        message.AttemptCount = nextAttemptCount;
        message.LastError = error.Length > 1000 ? error[..1000] : error;
        message.LockedBy = null;
        message.LockedUntil = null;
        message.RowVersion = Guid.NewGuid();

        if (nextAttemptCount >= message.MaxAttempts)
        {
            message.Status = OutboxMessageStatus.Failed;
        }
        else
        {
            message.Status = OutboxMessageStatus.Pending;
            var delaySeconds = Math.Min(300, Math.Pow(2, nextAttemptCount));
            message.NextAttemptAt = now.AddSeconds(delaySeconds);
        }

        await db.SaveChangesAsync(cancellationToken);
        return new OutboxFailureLogInfo(
            message.AttemptCount,
            message.MaxAttempts,
            message.Status,
            message.NextAttemptAt);
    }

    private static RewriteJob CreateRewriteJob(OutboxMessage message)
    {
        if (!string.Equals(message.MessageType, "RewriteJobCreated", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Unsupported outbox message type: {message.MessageType}");
        }

        var payload = JsonSerializer.Deserialize<RewriteJobCreatedPayload>(
            message.PayloadJson,
            JsonOptions);
        if (payload is null || payload.AttemptId == Guid.Empty)
        {
            throw new JsonException("Outbox payload did not contain a valid attempt id.");
        }

        return new RewriteJob(payload.AttemptId);
    }

    private sealed record RewriteJobCreatedPayload(Guid AttemptId);

    private sealed record OutboxFailureLogInfo(
        int AttemptCount,
        int MaxAttempts,
        OutboxMessageStatus Status,
        DateTimeOffset NextAttemptAt);
}
