using ReplyInMyVoice.Application.Abstractions;

namespace ReplyInMyVoice.Application.UseCases.Admin;

public sealed class AdminRetryWebhookDeliveryHandler(
    IWebhookDeliveryRepository webhookDeliveries,
    IUnitOfWork unitOfWork)
{
    public async Task<AdminRetryWebhookDeliveryResultDto> HandleAsync(
        AdminRetryWebhookDeliveryCommand command,
        CancellationToken ct = default)
    {
        var result = await webhookDeliveries.RetryFailedDeliveryAsync(
            command.DeliveryId,
            command.Now,
            ct);

        if (result.Kind == WebhookDeliveryRetryResultKind.Success)
        {
            await unitOfWork.SaveChangesAsync(ct);
        }

        return result.Kind switch
        {
            WebhookDeliveryRetryResultKind.Success => new AdminRetryWebhookDeliveryResultDto(
                AdminRetryWebhookDeliveryResultKind.Success,
                result.DeliveryId,
                result.NextAttemptAt),
            WebhookDeliveryRetryResultKind.InvalidState => new AdminRetryWebhookDeliveryResultDto(
                AdminRetryWebhookDeliveryResultKind.InvalidState,
                result.DeliveryId,
                result.NextAttemptAt,
                "Webhook delivery is not failed."),
            _ => new AdminRetryWebhookDeliveryResultDto(
                AdminRetryWebhookDeliveryResultKind.NotFound,
                Detail: "Webhook delivery was not found."),
        };
    }
}

public sealed record AdminRetryWebhookDeliveryResultDto(
    AdminRetryWebhookDeliveryResultKind Kind,
    Guid? DeliveryId = null,
    DateTimeOffset? NextAttemptAt = null,
    string? Detail = null);

public enum AdminRetryWebhookDeliveryResultKind
{
    Success = 0,
    NotFound = 1,
    InvalidState = 2,
}
