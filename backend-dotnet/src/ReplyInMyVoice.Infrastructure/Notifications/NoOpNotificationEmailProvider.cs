using Microsoft.Extensions.Logging;

namespace ReplyInMyVoice.Infrastructure.Notifications;

public sealed class NoOpNotificationEmailProvider(
    string reason,
    ILogger<NoOpNotificationEmailProvider> logger) : INotificationEmailProvider
{
    public Task<NotificationSendResult> SendAsync(
        NotificationEmail email,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Skipping notification template {TemplateName}; notification email provider is not active: {Reason}.",
            email.TemplateName,
            reason);

        return Task.FromResult(NotificationSendResult.Skipped("noop", reason));
    }
}
