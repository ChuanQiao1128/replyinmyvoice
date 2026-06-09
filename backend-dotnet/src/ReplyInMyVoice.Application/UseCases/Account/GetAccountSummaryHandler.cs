using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.Common;
using ReplyInMyVoice.Domain.Entities;

namespace ReplyInMyVoice.Application.UseCases.Account;

public sealed class GetAccountSummaryHandler(
    IAppUserRepository appUsers,
    IUsagePeriodRepository usagePeriods,
    IRewriteCreditRepository credits,
    IPromoCodeRedemptionRepository promoRedemptions,
    IPromoCodeRepository promoCodes,
    IAccountUsagePlanProvider usagePlans,
    IUnitOfWork unitOfWork)
{
    public async Task<AccountSummaryDto> HandleAsync(
        GetAccountSummaryQuery query,
        CancellationToken ct = default)
    {
        var user = await AccountUseCaseSupport.GetOrCreateUserAsync(
            appUsers,
            unitOfWork,
            query.ExternalAuthUserId,
            query.Email,
            ct);
        var usagePlan = usagePlans.GetUsagePlan(user);
        var period = await usagePeriods.GetByUserIdAndPeriodKeyAsync(user.Id, usagePlan.PeriodKey, ct);
        var used = period?.UsedCount ?? 0;
        var reserved = period?.ReservedCount ?? 0;
        var periodRemaining = Math.Max(usagePlan.QuotaLimit - used - reserved, 0);
        var now = DateTimeOffset.UtcNow;
        var userCredits = await credits.ListByUserIdAsync(user.Id, ct);
        var activeCredits = userCredits
            .Where(x => x.ExpiresAt is null || x.ExpiresAt > now)
            .ToList();
        var creditRemaining = activeCredits.Sum(x => Math.Max(x.AmountGranted - x.AmountConsumed, 0));
        var creditUsed = activeCredits.Sum(x => Math.Max(x.AmountConsumed, 0));
        var activePromoCredits = activeCredits
            .Where(x => x.Source == "PROMO")
            .ToList();
        var trialRemaining = activePromoCredits.Sum(x => Math.Max(x.AmountGranted - x.AmountConsumed, 0));
        var trialExpiresAt = activePromoCredits
            .Where(x => x.ExpiresAt is not null && x.AmountGranted - x.AmountConsumed > 0)
            .Select(x => x.ExpiresAt!.Value)
            .OrderBy(x => x)
            .Cast<DateTimeOffset?>()
            .FirstOrDefault();
        var hasRedeemedPromo = await promoRedemptions.HasAppliedForUserAsync(user.Id, ct);
        var allPromoCodes = await promoCodes.ListAllAsync(ct);
        var hasRedeemableCode = allPromoCodes.Any(x =>
            x.IsActive &&
            x.ValidFrom <= now &&
            now <= x.ValidUntil &&
            (x.MaxRedemptionsGlobal is null || x.RedemptionCount < x.MaxRedemptionsGlobal));
        var remaining = periodRemaining + creditRemaining;
        var totalUsed = used + creditUsed;
        var quota = totalUsed + reserved + remaining;
        var sources = new List<AccountUsageSourceDto>
        {
            new(
                usagePlan.Scope,
                usagePlan.Scope == "paid" ? "Included rewrites" : "Free rewrites",
                used,
                usagePlan.QuotaLimit,
                reserved,
                periodRemaining,
                null,
                null),
        };
        sources.AddRange(activeCredits.Select(x => new AccountUsageSourceDto(
            x.Source,
            AccountUseCaseSupport.LabelForCreditSource(x.Source),
            x.AmountConsumed,
            x.AmountGranted,
            0,
            Math.Max(x.AmountGranted - x.AmountConsumed, 0),
            x.ExpiresAt,
            AccountUseCaseSupport.CalculateExpiresInDays(x.ExpiresAt, now))));

        return new AccountSummaryDto(
            user.Id,
            user.ExternalAuthUserId,
            user.Email,
            user.SubscriptionStatus.ToString(),
            user.PaymentGraceEndsAt,
            new AccountUsageSummaryDto(
                usagePlan.Scope,
                usagePlan.PeriodKey,
                quota,
                totalUsed,
                reserved,
                remaining,
                remaining <= 0)
            {
                Sources = sources,
            },
            new AccountPromoSummaryDto(
                hasRedeemedPromo,
                !hasRedeemedPromo && hasRedeemableCode,
                trialRemaining,
                trialExpiresAt));
    }
}
