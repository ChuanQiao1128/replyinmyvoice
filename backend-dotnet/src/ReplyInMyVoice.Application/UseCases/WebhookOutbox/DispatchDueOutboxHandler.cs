using System.Data;
using ReplyInMyVoice.Application.Abstractions;

namespace ReplyInMyVoice.Application.UseCases.WebhookOutbox;

public sealed class DispatchDueOutboxHandler(
    IOutboxMessageRepository outboxMessages,
    IEnumerable<IOutboxMessageHandler> messageHandlers,
    IUnitOfWork unitOfWork)
{
    private const int ClaimRaceMaxAttempts = 5;
    private static readonly TimeSpan ClaimLease = TimeSpan.FromSeconds(30);

    private readonly Dictionary<string, IOutboxMessageHandler> _messageHandlers = messageHandlers
        .ToDictionary(x => x.MessageType, StringComparer.Ordinal);

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
            try
            {
                if (!_messageHandlers.TryGetValue(message.MessageType, out var handler))
                {
                    throw new InvalidOperationException($"Unsupported outbox message type: {message.MessageType}");
                }

                await handler.HandleAsync(message, ct);
                await outboxMessages.MarkSentAsync(message.Id, command.Now, ct);
                await unitOfWork.SaveChangesAsync(ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await outboxMessages.MarkFailedAttemptAsync(message.Id, command.Now, ex.Message, ct);
                await unitOfWork.SaveChangesAsync(ct);
            }
        }

        return messages.Count;
    }
}
