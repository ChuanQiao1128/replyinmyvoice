using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Infrastructure.Data;

namespace ReplyInMyVoice.Infrastructure.Repositories;

public sealed class OutboxMessageRepository(AppDbContext db) : IOutboxMessageRepository
{
    public async Task AddAsync(OutboxMessage message, CancellationToken ct = default)
    {
        await db.OutboxMessages.AddAsync(message, ct);
    }
}
