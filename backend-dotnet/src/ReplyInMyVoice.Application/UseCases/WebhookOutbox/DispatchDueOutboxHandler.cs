using System.Data;
using ReplyInMyVoice.Application.Abstractions;

namespace ReplyInMyVoice.Application.UseCases.WebhookOutbox;

public sealed class DispatchDueOutboxHandler(
    IOutboxMessageRepository outboxMessages,
    IEnumerable<IOutboxMessageHandler> messageHandlers,
    IOutboxDispatchObserver dispatchObserver,
    IUnitOfWork unitOfWork,
    IBusinessMetrics? metrics = null)
{
    private const int ClaimRaceMaxAttempts = 5;
    private static readonly TimeSpan ClaimLease = TimeSpan.FromSeconds(30);

    private readonly Dictionary<string, IOutboxMessageHandler> _messageHandlers = messageHandlers
        .ToDictionary(x => x.MessageType, StringComparer.Ordinal);
    private readonly IBusinessMetrics _metrics = metrics ?? NoOpBusinessMetrics.Instance;

    public async Task<int> HandleAsync(
        DispatchDueOutboxCommand command,
        CancellationToken ct = default)
    {
        var messages = await unitOfWork.ExecuteInTransactionAsync(
            async transactionCt =>
            {
                var claimed = await outboxMessages.ClaimDueAsync(
                    command.Now,
                    command.LockedBy,
                    command.BatchSize,
                    ClaimLease,
                    transactionCt);
                await unitOfWork.SaveChangesAsync(transactionCt);
                return claimed;
            },
            IsolationLevel.Serializable,
            ClaimRaceMaxAttempts,
            ct);

        foreach (var message in messages)
        {
            await DispatchClaimedMessageAsync(message, command.Now, ct);
        }

        var oldestIncompleteCreatedAt = await outboxMessages.GetOldestIncompleteCreatedAtAsync(ct);
        _metrics.Record(
            BusinessMetricNames.OutboxBacklogAgeSeconds,
            oldestIncompleteCreatedAt is { } oldest
                ? Math.Max(0, (command.Now - oldest).TotalSeconds)
                : 0);

        return messages.Count;
    }

    public async Task<bool> TryDispatchOneAsync(
        DispatchOutboxMessageCommand command,
        CancellationToken ct = default)
    {
        var message = await unitOfWork.ExecuteInTransactionAsync(
            async transactionCt =>
            {
                var claimed = await outboxMessages.ClaimByIdAsync(
                    command.OutboxMessageId,
                    command.Now,
                    command.LockedBy,
                    ClaimLease,
                    transactionCt);
                if (claimed is not null)
                {
                    await unitOfWork.SaveChangesAsync(transactionCt);
                }

                return claimed;
            },
            IsolationLevel.Serializable,
            ClaimRaceMaxAttempts,
            ct);

        if (message is null)
        {
            return false;
        }

        return await DispatchClaimedMessageAsync(message, command.Now, ct);
    }

    private async Task<bool> DispatchClaimedMessageAsync(
        ReplyInMyVoice.Domain.Entities.OutboxMessage message,
        DateTimeOffset now,
        CancellationToken ct)
    {
        try
        {
            if (!_messageHandlers.TryGetValue(message.MessageType, out var handler))
            {
                throw new InvalidOperationException($"Unsupported outbox message type: {message.MessageType}");
            }

            await handler.HandleAsync(message, ct);
            await outboxMessages.MarkSentAsync(message.Id, now, ct);
            await unitOfWork.SaveChangesAsync(ct);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var failure = await outboxMessages.MarkFailedAttemptAsync(message.Id, now, ex.Message, ct);
            await unitOfWork.SaveChangesAsync(ct);
            _metrics.Record(
                BusinessMetricNames.OutboxFailedTotal,
                1,
                BusinessMetricDimensions.MessageType,
                message.MessageType);
            if (failure.Status == ReplyInMyVoice.Domain.Enums.OutboxMessageStatus.Failed)
            {
                await dispatchObserver.OnTerminalFailureAsync(message, ex.Message, ct);
            }

            return false;
        }
    }
}
