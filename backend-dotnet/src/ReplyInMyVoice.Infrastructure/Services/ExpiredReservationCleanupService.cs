namespace ReplyInMyVoice.Infrastructure.Services;

public sealed class ExpiredReservationCleanupService(QuotaService quotaService)
{
    public Task<int> RunOnceAsync(DateTimeOffset now, CancellationToken cancellationToken = default) =>
        quotaService.ReleaseExpiredReservationsAsync(now, cancellationToken);
}
