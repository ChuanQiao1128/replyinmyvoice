using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.Common;

namespace ReplyInMyVoice.Application.UseCases.Billing;

public sealed class CreatePortalSessionHandler(
    IAppUserRepository appUsers,
    IStripeBillingClient stripeBillingClient)
{
    public async Task<PortalSessionDto> HandleAsync(
        CreatePortalSessionQuery query,
        CancellationToken ct = default)
    {
        var externalAuthUserId = BillingUseCaseSupport.NormalizeExternalAuthUserId(query.ExternalAuthUserId);
        var user = await appUsers.GetByExternalAuthUserIdAsync(externalAuthUserId, ct);

        if (string.IsNullOrWhiteSpace(user?.StripeCustomerId))
        {
            throw new InvalidOperationException("stripe_customer_missing");
        }

        var session = await stripeBillingClient.CreatePortalSessionAsync(
            user.StripeCustomerId,
            ct);

        return new PortalSessionDto(
            session.Url ?? throw new InvalidOperationException("stripe_portal_url_missing"));
    }
}
