using ReplyInMyVoice.Application.UseCases.Quota;

namespace ReplyInMyVoice.Infrastructure.Services;

public sealed class ExpiredReservationCleanupService(ReleaseExpiredReservationsHandler handler)
{
    public Task<int> RunOnceAsync(DateTimeOffset now, CancellationToken cancellationToken = default) =>
        handler.HandleAsync(new ReleaseExpiredReservationsCommand(now), cancellationToken);
}
