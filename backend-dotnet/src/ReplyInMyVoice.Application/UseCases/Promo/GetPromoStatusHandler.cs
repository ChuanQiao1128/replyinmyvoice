using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.Common;
using ReplyInMyVoice.Application.UseCases.Account;

namespace ReplyInMyVoice.Application.UseCases.Promo;

public sealed class GetPromoStatusHandler(
    IAppUserRepository appUsers,
    IPromoCodeRedemptionRepository redemptions,
    IPromoCodeRepository promoCodes,
    IRewriteCreditRepository credits,
    IUnitOfWork unitOfWork)
{
    private const string PromoCreditSource = "PROMO";

    public async Task<PromoStatusDto> HandleAsync(
        GetPromoStatusQuery query,
        CancellationToken ct = default)
    {
        var user = await AccountUseCaseSupport.GetOrCreateUserAsync(
            appUsers,
            unitOfWork,
            query.ExternalAuthUserId,
            query.Email,
            ct);

        var hasRedeemed = await redemptions.HasAppliedForUserAsync(user.Id, ct);
        var userCredits = await credits.ListByUserIdAsync(user.Id, ct);
        var activePromoCredits = userCredits
            .Where(x => x.Source == PromoCreditSource && (x.ExpiresAt is null || x.ExpiresAt > query.Now))
            .ToList();
        var trialRemaining = activePromoCredits.Sum(x => Math.Max(x.AmountGranted - x.AmountConsumed, 0));
        var trialExpiresAt = activePromoCredits
            .Where(x => x.ExpiresAt is not null && x.AmountGranted - x.AmountConsumed > 0)
            .Select(x => x.ExpiresAt!.Value)
            .OrderBy(x => x)
            .Cast<DateTimeOffset?>()
            .FirstOrDefault();
        var allPromoCodes = await promoCodes.ListAllAsync(ct);
        var hasRedeemableCode = allPromoCodes.Any(x =>
            x.IsActive &&
            x.ValidFrom <= query.Now &&
            query.Now <= x.ValidUntil &&
            (x.MaxRedemptionsGlobal is null || x.RedemptionCount < x.MaxRedemptionsGlobal));

        return new PromoStatusDto(
            hasRedeemed,
            !hasRedeemed && hasRedeemableCode,
            trialRemaining,
            trialExpiresAt);
    }
}
