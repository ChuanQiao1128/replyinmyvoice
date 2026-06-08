using ReplyInMyVoice.Domain.Entities;

namespace ReplyInMyVoice.Application.Abstractions;

public interface IOutboxMessageRepository
{
    Task AddAsync(OutboxMessage message, CancellationToken ct = default);
}
