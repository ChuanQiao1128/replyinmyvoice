namespace ReplyInMyVoice.Infrastructure.Services;

public interface IStripeBillingService
{
    Task<string> CreateCheckoutSessionUrlAsync(
        string externalAuthUserId,
        string? email,
        CancellationToken cancellationToken);

    Task<string> CreatePortalSessionUrlAsync(
        string externalAuthUserId,
        CancellationToken cancellationToken);
}
