using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Infrastructure.Data;

namespace ReplyInMyVoice.Infrastructure.Repositories;

public sealed class UsageReservationRepository(AppDbContext db) : IUsageReservationRepository
{
    public async Task AddAsync(UsageReservation reservation, CancellationToken ct = default)
    {
        await db.UsageReservations.AddAsync(reservation, ct);
    }
}
