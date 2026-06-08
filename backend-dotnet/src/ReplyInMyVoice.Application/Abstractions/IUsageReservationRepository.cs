using ReplyInMyVoice.Domain.Entities;

namespace ReplyInMyVoice.Application.Abstractions;

public interface IUsageReservationRepository
{
    Task AddAsync(UsageReservation reservation, CancellationToken ct = default);
}
