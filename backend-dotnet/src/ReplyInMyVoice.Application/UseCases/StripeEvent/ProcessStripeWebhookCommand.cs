using ReplyInMyVoice.Application.Common;

namespace ReplyInMyVoice.Application.UseCases.StripeEvent;

public sealed record ProcessStripeWebhookCommand(
    StripeWebhookPayloadDto Payload,
    DateTimeOffset Now);
