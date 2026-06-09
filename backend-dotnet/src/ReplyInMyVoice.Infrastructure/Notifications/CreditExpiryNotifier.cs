using Microsoft.Extensions.Configuration;
using ReplyInMyVoice.Application.Abstractions;

namespace ReplyInMyVoice.Infrastructure.Notifications;

public sealed class CreditExpiryNotifier(
    IConfiguration configuration,
    INotificationService notifications) : ICreditExpiryNotifier
{
    public async Task<bool> TrySendCreditExpiringAsync(
        CreditExpiryNotificationRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.RecipientEmail) || request.CreditsExpiring <= 0)
        {
            return false;
        }

        var result = await notifications.SendAsync(
            NotificationTemplates.CreditExpiring,
            new NotificationRecipient(request.RecipientEmail),
            new CreditExpiringNotificationModel(
                CustomerName: string.Empty,
                SupportEmail: ResolveSupportEmail(),
                CreditsExpiring: request.CreditsExpiring,
                ExpiresOnUtc: request.ExpiresOnUtc.ToUniversalTime()),
            ct);

        return result.Sent;
    }

    private string ResolveSupportEmail() =>
        FirstConfiguredValue(
            "NOTIFICATIONS_REPLY_TO_EMAIL",
            "NOTIFICATIONS_SUPPORT_EMAIL",
            "SUPPORT_EMAIL") ?? "info@timeawake.co.nz";

    private string? FirstConfiguredValue(params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = configuration[key];
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }
}
