using Microsoft.Extensions.Logging;

namespace ReplyInMyVoice.Infrastructure.Notifications;

public sealed class NotificationService(
    INotificationEmailProvider emailProvider,
    ILogger<NotificationService> logger) : INotificationService
{
    public Task<NotificationSendResult> SendAsync<TModel>(
        NotificationTemplate<TModel> template,
        NotificationRecipient recipient,
        TModel model,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(recipient.Email))
        {
            logger.LogWarning(
                "Skipping notification template {TemplateName} because recipient email is missing.",
                template.Name);
            return Task.FromResult(NotificationSendResult.Skipped("validation", "missing_recipient_email"));
        }

        var rendered = template.Render(model);
        var email = new NotificationEmail(
            template.Name,
            recipient,
            rendered.Subject,
            rendered.PlainTextBody,
            rendered.HtmlBody);

        return emailProvider.SendAsync(email, cancellationToken);
    }
}
