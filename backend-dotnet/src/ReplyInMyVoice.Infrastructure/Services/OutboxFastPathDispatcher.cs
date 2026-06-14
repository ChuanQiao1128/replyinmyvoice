using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.UseCases.WebhookOutbox;

namespace ReplyInMyVoice.Infrastructure.Services;

public sealed record OutboxFastPathOptions(bool Enabled, TimeSpan Timeout);

public sealed class OutboxFastPathDispatcher(
    DispatchDueOutboxHandler dispatchDueOutboxHandler,
    OutboxFastPathOptions options,
    ILogger<OutboxFastPathDispatcher> logger) : IOutboxFastPathDispatcher
{
    public async Task TryDispatchAsync(Guid outboxMessageId, CancellationToken ct = default)
    {
        if (!options.Enabled)
        {
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var timeoutCts = new CancellationTokenSource(options.Timeout);
            var sent = await dispatchDueOutboxHandler.TryDispatchOneAsync(
                new DispatchOutboxMessageCommand(
                    outboxMessageId,
                    DateTimeOffset.UtcNow,
                    $"{Environment.MachineName}:fastpath"),
                timeoutCts.Token);

            if (sent)
            {
                logger.LogInformation(
                    "Outbox fast-path dispatched message {OutboxMessageId} in {ElapsedMs} ms.",
                    outboxMessageId,
                    stopwatch.ElapsedMilliseconds);
            }
            else
            {
                logger.LogInformation(
                    "Outbox fast-path skipped message {OutboxMessageId} in {ElapsedMs} ms; timer dispatcher will deliver.",
                    outboxMessageId,
                    stopwatch.ElapsedMilliseconds);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Outbox fast-path dispatch failed for message {OutboxMessageId}; timer dispatcher will retry.",
                outboxMessageId);
        }
    }
}
