namespace ReplyInMyVoice.Application.Abstractions;

public interface IOutboxFastPathDispatcher
{
    // Best-effort post-commit dispatch hook; implementations must never throw.
    Task TryDispatchAsync(Guid outboxMessageId, CancellationToken ct = default);
}
