using ReplyInMyVoice.Application.Abstractions;

namespace ReplyInMyVoice.Application.UseCases.Account;

public sealed class HasPaidApiEntitlementHandler(
    IAppUserRepository appUsers,
    IRewriteCreditRepository credits)
{
    public async Task<bool> HandleAsync(
        HasPaidApiEntitlementQuery query,
        CancellationToken ct = default)
    {
        var user = await appUsers.GetByIdAsync(query.UserId, ct);
        if (user is null)
        {
            return false;
        }

        if (AccountUseCaseSupport.IsPaidApiSubscriptionStatus(user.SubscriptionStatus))
        {
            return true;
        }

        var userCredits = await credits.ListByUserIdAsync(query.UserId, ct);
        return userCredits.Any(x =>
            x.Source == "PURCHASE" &&
            (x.ExpiresAt is null || x.ExpiresAt > query.Now) &&
            x.AmountGranted - x.AmountConsumed > 0);
    }
}
