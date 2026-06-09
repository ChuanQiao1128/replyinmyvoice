using Microsoft.Extensions.Configuration;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.Common;

namespace ReplyInMyVoice.Infrastructure.Notifications;

public sealed class TaxTurnoverNotifier(
    IConfiguration configuration,
    INotificationService notificationService) : ITaxTurnoverNotifier
{
    public async Task<TaxTurnoverNotificationResultDto> TrySendWarningNotificationAsync(
        TaxTurnoverNotificationRequest request,
        CancellationToken ct = default)
    {
        var recipientEmail = configuration["GST_TURNOVER_NOTIFICATION_EMAIL"]?.Trim();
        if (string.IsNullOrWhiteSpace(recipientEmail))
        {
            return new TaxTurnoverNotificationResultDto(
                Attempted: false,
                Sent: false,
                Provider: null,
                Reason: "notification_not_configured");
        }

        var result = await notificationService.SendAsync(
            NotificationTemplates.GstTurnoverThreshold,
            new NotificationRecipient(
                recipientEmail,
                configuration["GST_TURNOVER_NOTIFICATION_NAME"]?.Trim()),
            new GstTurnoverThresholdNotificationModel(
                GrossAmountTotal: request.GrossAmountTotal,
                RegistrationThresholdAmountTotal: request.RegistrationThresholdAmountTotal,
                WarningFraction: request.WarningFraction,
                WindowEndUtc: request.WindowEnd,
                SupportEmail: configuration["NOTIFICATIONS_REPLY_TO_EMAIL"]),
            ct);

        return new TaxTurnoverNotificationResultDto(
            Attempted: true,
            Sent: result.Sent,
            Provider: result.Provider,
            Reason: result.Reason);
    }
}
