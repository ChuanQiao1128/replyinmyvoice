using ReplyInMyVoice.Domain.Entities;

namespace ReplyInMyVoice.Application.Abstractions;

/// <summary>
/// Observes terminal outbox dispatch failures. Implementations must not throw.
/// </summary>
public interface IOutboxDispatchObserver
{
    Task OnTerminalFailureAsync(
        OutboxMessage message,
        string error,
        CancellationToken ct = default);
}
