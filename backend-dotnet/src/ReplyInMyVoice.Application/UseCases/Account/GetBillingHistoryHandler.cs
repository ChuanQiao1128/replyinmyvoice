using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.Common;

namespace ReplyInMyVoice.Application.UseCases.Account;

public sealed class GetBillingHistoryHandler(
    IAppUserRepository appUsers,
    IRewriteCreditRepository credits,
    IStripeInvoiceRepository invoices,
    IUnitOfWork unitOfWork)
{
    public async Task<IReadOnlyList<AccountBillingHistoryItemDto>> HandleAsync(
        GetBillingHistoryQuery query,
        CancellationToken ct = default)
    {
        var user = await AccountUseCaseSupport.GetOrCreateUserAsync(
            appUsers,
            unitOfWork,
            query.ExternalAuthUserId,
            query.Email,
            ct);
        var userCredits = await credits.ListByUserIdAsync(user.Id, ct);
        var purchases = userCredits
            .Where(x => x.Source == "PURCHASE")
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
        var userInvoices = await invoices.ListByUserIdAsync(user.Id, ct);
        var refundedCredits = userCredits
            .Where(x =>
                x.Source == "PURCHASE" &&
                x.OriginalAmountGranted is not null &&
                x.OriginalAmountGranted > x.AmountGranted)
            .ToList();

        var history = new List<AccountBillingHistoryItemDto>();
        history.AddRange(purchases.Select(x => new AccountBillingHistoryItemDto(
            "pack",
            x.Date,
            string.IsNullOrWhiteSpace(x.Sku) ? "Credit pack" : x.Sku!,
            x.Amount,
            x.Currency,
            "paid",
            x.ReceiptUrl,
            null)));
        history.AddRange(userInvoices.Select(x => new AccountBillingHistoryItemDto(
            "subscription",
            x.CreatedAt,
            AccountUseCaseSupport.FormatSubscriptionInvoiceDescription(x.PeriodStart, x.PeriodEnd),
            x.AmountPaid > 0 ? x.AmountPaid : x.AmountDue,
            x.Currency,
            x.Status,
            null,
            x.HostedInvoiceUrl)));
        history.AddRange(refundedCredits.Select(x => new AccountBillingHistoryItemDto(
            "refund",
            x.GrantedAt,
            AccountUseCaseSupport.FormatRefundDescription(x.StripeSku),
            AccountUseCaseSupport.CalculateRefundAmount(
                x.StripeAmountTotal,
                x.OriginalAmountGranted,
                x.AmountGranted),
            x.StripeCurrency,
            "refunded",
            null,
            null)));

        return history
            .OrderByDescending(x => x.Date)
            .ThenBy(x => x.Type, StringComparer.Ordinal)
            .ToList();
    }
}
