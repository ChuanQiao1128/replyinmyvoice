namespace ReplyInMyVoice.Application.Abstractions;

public interface IStripeRefundClient
{
    Task<StripeRefundResult> RefundPaymentAsync(
        StripeRefundRequest request,
        CancellationToken ct = default);
}
