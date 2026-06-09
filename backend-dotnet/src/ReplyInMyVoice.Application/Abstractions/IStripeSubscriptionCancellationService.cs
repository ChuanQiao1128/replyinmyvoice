namespace ReplyInMyVoice.Application.Abstractions;

public interface IStripeSubscriptionCancellationService
{
    Task CancelSubscriptionAsync(string stripeSubscriptionId, CancellationToken ct = default);
}
