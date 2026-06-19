using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Domain.Enums;

namespace ReplyInMyVoice.Application.UseCases.Admin;

public sealed class RetryWebhookDeliveryHandler(
    IWebhookDeliveryRepository webhookDeliveries,
    IUnitOfWork unitOfWork)
{
    public async Task<AdminWebhookDeliveryRetryResultDto> HandleAsync(
        RetryWebhookDeliveryCommand command,
        CancellationToken ct = default)
    {
        var result = await webhookDeliveries.RetryFailedAsync(command.DeliveryId, command.Now, ct);
        if (result.Kind == WebhookDeliveryRetryResultKind.Success)
        {
            await unitOfWork.SaveChangesAsync(ct);
        }

        return result.Kind switch
        {
            WebhookDeliveryRetryResultKind.Success => AdminWebhookDeliveryRetryResultDto.Success(
                result.Delivery!.Id,
                result.Delivery.Status,
                result.Delivery.AttemptCount,
                result.Delivery.NextAttemptAt),
            WebhookDeliveryRetryResultKind.NotFailed => AdminWebhookDeliveryRetryResultDto.NotFailed(
                result.Delivery!.Id,
                result.Delivery.Status),
            _ => AdminWebhookDeliveryRetryResultDto.NotFound(),
        };
    }
}

public sealed record RetryWebhookDeliveryCommand(
    Guid DeliveryId,
    DateTimeOffset Now);

public sealed record AdminWebhookDeliveryRetryResultDto(
    AdminWebhookDeliveryRetryResultKind Kind,
    Guid? Id,
    WebhookDeliveryStatus? Status,
    int? AttemptCount,
    DateTimeOffset? NextAttemptAt)
{
    public static AdminWebhookDeliveryRetryResultDto Success(
        Guid id,
        WebhookDeliveryStatus status,
        int attemptCount,
        DateTimeOffset nextAttemptAt) =>
        new(
            AdminWebhookDeliveryRetryResultKind.Success,
            id,
            status,
            attemptCount,
            nextAttemptAt);

    public static AdminWebhookDeliveryRetryResultDto NotFailed(
        Guid id,
        WebhookDeliveryStatus status) =>
        new(
            AdminWebhookDeliveryRetryResultKind.NotFailed,
            id,
            status,
            null,
            null);

    public static AdminWebhookDeliveryRetryResultDto NotFound() =>
        new(
            AdminWebhookDeliveryRetryResultKind.NotFound,
            null,
            null,
            null,
            null);
}

public enum AdminWebhookDeliveryRetryResultKind
{
    Success = 0,
    NotFound = 1,
    NotFailed = 2,
}
