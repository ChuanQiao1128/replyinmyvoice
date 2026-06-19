namespace ReplyInMyVoice.Infrastructure.Notifications;

public interface INotificationService
{
    Task<NotificationSendResult> SendAsync<TModel>(
        NotificationTemplate<TModel> template,
        NotificationRecipient recipient,
        TModel model,
        CancellationToken cancellationToken = default,
        Guid? outboxMessageId = null);
}

public interface INotificationEmailProvider
{
    Task<NotificationSendResult> SendAsync(
        NotificationEmail email,
        CancellationToken cancellationToken = default);
}

public sealed record NotificationRecipient(
    string Email,
    string? DisplayName = null);

public sealed record NotificationEmail(
    string TemplateName,
    NotificationRecipient Recipient,
    string Subject,
    string PlainTextBody,
    string HtmlBody,
    Guid? OutboxMessageId = null);

public sealed record NotificationSendResult(
    bool Sent,
    string Provider,
    string? OperationId = null,
    string? Reason = null)
{
    public static NotificationSendResult Delivered(string provider, string? operationId = null) =>
        new(true, provider, operationId);

    public static NotificationSendResult Skipped(string provider, string reason) =>
        new(false, provider, null, reason);
}

public sealed record NotificationTemplate<TModel>(
    string Name,
    Func<TModel, RenderedNotification> Render);

public sealed record RenderedNotification(
    string Subject,
    string PlainTextBody,
    string HtmlBody);
