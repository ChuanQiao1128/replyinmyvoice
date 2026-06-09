using Microsoft.Extensions.Configuration;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.Common;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;

namespace ReplyInMyVoice.Infrastructure.Services;

public sealed class AccountUsagePlanProvider(IConfiguration configuration) : IAccountUsagePlanProvider
{
    private const int DefaultFreeBaselineRewrites = 0;

    public AccountUsagePlanDto GetUsagePlan(AppUser user)
    {
        if (user.SubscriptionStatus is SubscriptionStatus.Active or SubscriptionStatus.Trialing or SubscriptionStatus.Testing or SubscriptionStatus.PastDue)
        {
            return new AccountUsagePlanDto(
                "paid",
                $"paid:{user.StripeSubscriptionId ?? user.Id.ToString()}:{user.CurrentPeriodEnd?.ToString("O") ?? "no-period"}",
                user.SubscriptionStatus == SubscriptionStatus.Testing ? 10_000 : 90);
        }

        return new AccountUsagePlanDto(
            "free",
            "free:lifetime",
            ResolveFreeBaselineRewrites(configuration));
    }

    private static int ResolveFreeBaselineRewrites(IConfiguration configuration)
    {
        var configuredValue = configuration["FREE_BASELINE_REWRITES"];
        return int.TryParse(configuredValue, out var parsed) && parsed >= 0
            ? parsed
            : DefaultFreeBaselineRewrites;
    }
}
