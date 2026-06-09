using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.Common;

namespace ReplyInMyVoice.Application.UseCases.Account;

public sealed class GetPurchaseHistoryHandler(
    IAppUserRepository appUsers,
    IRewriteCreditRepository credits,
    IUnitOfWork unitOfWork)
{
    public async Task<IReadOnlyList<AccountPaymentDto>> HandleAsync(
        GetPurchaseHistoryQuery query,
        CancellationToken ct = default)
    {
        var user = await AccountUseCaseSupport.GetOrCreateUserAsync(
            appUsers,
            unitOfWork,
            query.ExternalAuthUserId,
            query.Email,
            ct);
        var userCredits = await credits.ListByUserIdAsync(user.Id, ct);

        return userCredits
            .Where(x => x.Source == "PURCHASE")
            .OrderByDescending(x => x.GrantedAt)
            .Select(x => new AccountPaymentDto(
                x.StripeSku,
                x.StripePaymentIntentId,
                x.StripeAmountTotal,
                x.StripeCurrency,
                x.StripeReceiptUrl,
                x.GrantedAt,
                x.ExpiresAt,
                Math.Max(x.AmountGranted - x.AmountConsumed, 0)))
            .ToList();
    }
}
