namespace ReplyInMyVoice.Application.UseCases.Admin;

public sealed record AdminRetryWebhookDeliveryCommand(
    Guid DeliveryId,
    DateTimeOffset Now);
