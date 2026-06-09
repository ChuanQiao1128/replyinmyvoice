using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.Common;

namespace ReplyInMyVoice.Application.UseCases.Billing;

public sealed class CancelSubscriptionHandler(
    IAppUserRepository appUsers,
    IStripeBillingClient stripeBillingClient)
{
    public async Task<CancelSubscriptionResultDto> HandleAsync(
        CancelSubscriptionCommand command,
        CancellationToken ct = default)
    {
        var externalAuthUserId = BillingUseCaseSupport.NormalizeExternalAuthUserId(command.ExternalAuthUserId);
        var user = await appUsers.GetByExternalAuthUserIdAsync(externalAuthUserId, ct);
        var subscriptionId = user?.StripeSubscriptionId?.Trim();

        if (string.IsNullOrWhiteSpace(subscriptionId))
        {
            return new CancelSubscriptionResultDto(false, null);
        }

        await stripeBillingClient.CancelSubscriptionAsync(subscriptionId, ct);
        return new CancelSubscriptionResultDto(true, subscriptionId);
    }
}
