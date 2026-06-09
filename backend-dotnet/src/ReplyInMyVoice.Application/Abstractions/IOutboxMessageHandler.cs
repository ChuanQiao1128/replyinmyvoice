using ReplyInMyVoice.Domain.Entities;

namespace ReplyInMyVoice.Application.Abstractions;

public interface IOutboxMessageHandler
{
    string MessageType { get; }

    Task HandleAsync(OutboxMessage message, CancellationToken ct = default);
}
