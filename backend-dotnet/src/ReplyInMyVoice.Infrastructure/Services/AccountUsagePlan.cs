using Microsoft.Extensions.Configuration;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;

namespace ReplyInMyVoice.Infrastructure.Services;

public static class AccountUsagePlans
{
    private const int DefaultFreeBaselineRewrites = 0;

    public static AccountUsagePlan GetUsagePlan(AppUser user, IConfiguration? configuration = null)
    {
        // INTENTIONAL POLICY (do not "fix" as a bug): the consumer WEB path grants paid quota during the
        // PastDue dunning grace window (so a failed renewal charge doesn't instantly cut off a paying
        // customer — the grace is time-bounded by PaymentGraceEndsAt + the grace-expiry job). The paid B2B
        // API is deliberately STRICTER and excludes PastDue (see HasPaidApiEntitlementHandler /
        // AccountUseCaseSupport.IsPaidApiSubscriptionStatus): API integrators must keep billing current.
        // Different customer types, different reasonable policies — this divergence is by design.
        if (user.SubscriptionStatus is SubscriptionStatus.Active or SubscriptionStatus.Trialing or SubscriptionStatus.Testing or SubscriptionStatus.PastDue)
        {
            return new AccountUsagePlan(
                "paid",
                $"paid:{user.StripeSubscriptionId ?? user.Id.ToString()}:{user.CurrentPeriodEnd?.ToString("O") ?? "no-period"}",
                user.SubscriptionStatus == SubscriptionStatus.Testing ? 10_000 : 90);
        }

        return new AccountUsagePlan(
            "free",
            "free:lifetime",
            ResolveFreeBaselineRewrites(configuration));
    }

    private static int ResolveFreeBaselineRewrites(IConfiguration? configuration)
    {
        var configuredValue = configuration?["FREE_BASELINE_REWRITES"];
        return int.TryParse(configuredValue, out var parsed) && parsed >= 0
            ? parsed
            : DefaultFreeBaselineRewrites;
    }
}

public sealed record AccountUsagePlan(string Scope, string PeriodKey, int QuotaLimit);
