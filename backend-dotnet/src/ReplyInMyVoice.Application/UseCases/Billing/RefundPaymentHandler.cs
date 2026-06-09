using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.Common;

namespace ReplyInMyVoice.Application.UseCases.Billing;

public sealed class RefundPaymentHandler(
    IAppUserRepository appUsers,
    IRewriteCreditRepository credits,
    IStripeBillingClient stripeBillingClient)
{
    public async Task<ApplicationResult<RefundPaymentResultDto>> HandleAsync(
        RefundPaymentCommand command,
        CancellationToken ct = default)
    {
        var paymentIntentId = BillingUseCaseSupport.NormalizePaymentIntentId(command.PaymentIntentId);
        var user = await appUsers.GetByIdAsync(command.TargetUserId, ct);
        if (user is null)
        {
            return ApplicationResult<RefundPaymentResultDto>.NotFound();
        }

        var payment = await credits.GetByUserIdAndPaymentIntentIdAsync(
            command.TargetUserId,
            paymentIntentId,
            ct);
        if (payment is null)
        {
            return ApplicationResult<RefundPaymentResultDto>.NotFound();
        }

        var amount = command.Amount ?? payment.StripeAmountTotal;
        if (amount is not > 0)
        {
            throw new InvalidOperationException("refund_amount_missing");
        }

        if (payment.StripeAmountTotal is > 0 && amount > payment.StripeAmountTotal.Value)
        {
            throw new InvalidOperationException("refund_amount_exceeds_payment_amount");
        }

        var requestedCurrency = BillingUseCaseSupport.NormalizeCurrency(command.Currency);
        var storedCurrency = BillingUseCaseSupport.NormalizeCurrency(payment.StripeCurrency);
        if (!string.IsNullOrWhiteSpace(requestedCurrency) &&
            !string.IsNullOrWhiteSpace(storedCurrency) &&
            !string.Equals(requestedCurrency, storedCurrency, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("refund_currency_mismatch");
        }

        var refund = await stripeBillingClient.RefundPaymentAsync(
            new StripeRefundRequest(
                paymentIntentId,
                amount.Value,
                requestedCurrency ?? storedCurrency,
                command.IdempotencyKey,
                command.TargetUserId),
            ct);

        return ApplicationResult<RefundPaymentResultDto>.Success(new RefundPaymentResultDto(
            refund.RefundId,
            refund.PaymentIntentId,
            refund.Amount,
            refund.Currency ?? requestedCurrency ?? storedCurrency,
            refund.Status));
    }
}
