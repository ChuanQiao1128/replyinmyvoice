using Microsoft.Extensions.Configuration;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;

namespace ReplyInMyVoice.Infrastructure.Services;

public static class AccountUsagePlans
{
    private const int DefaultFreeBaselineRewrites = 0;

    public static AccountUsagePlan GetUsagePlan(AppUser user, IConfiguration? configuration = null)
    {
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
