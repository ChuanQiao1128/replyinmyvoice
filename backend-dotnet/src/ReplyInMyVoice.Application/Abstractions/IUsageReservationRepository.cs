using ReplyInMyVoice.Domain.Entities;

namespace ReplyInMyVoice.Application.Abstractions;

public interface IUsageReservationRepository
{
    Task AddAsync(UsageReservation reservation, CancellationToken ct = default);

    Task<IReadOnlyList<UsageReservation>> ListByUserIdAsync(
        Guid userId,
        CancellationToken ct = default);
}
