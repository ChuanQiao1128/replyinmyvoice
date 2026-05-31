using System.Globalization;
using System.Net;

namespace ReplyInMyVoice.Infrastructure.Notifications;

public static class NotificationTemplates
{
    public static readonly NotificationTemplate<FailedPaymentNotificationModel> FailedPayment = new(
        "failed-payment",
        model =>
        {
            var name = SafeName(model.CustomerName);
            var supportEmail = SafeEmail(model.SupportEmail);
            var billingPortalUrl = SafeUrl(model.BillingPortalUrl);

            return new RenderedNotification(
                "Payment did not go through",
                $"""
                Hi {name},

                Your latest payment for Reply In My Voice did not go through.

                Please update your payment method in billing settings. If you have questions, contact {supportEmail}.
                """,
                $"""
                <p>Hi {Html(name)},</p>
                <p>Your latest payment for Reply In My Voice did not go through.</p>
                <p>Please update your payment method in <a href="{HtmlAttribute(billingPortalUrl)}">billing settings</a>. If you have questions, contact <a href="mailto:{HtmlAttribute(supportEmail)}">{Html(supportEmail)}</a>.</p>
                """);
        });

    public static readonly NotificationTemplate<CreditExpiringNotificationModel> CreditExpiring = new(
        "credit-expiring",
        model =>
        {
            var name = SafeName(model.CustomerName);
            var supportEmail = SafeEmail(model.SupportEmail);
            var credits = Math.Max(0, model.CreditsExpiring);
            var expiresOn = model.ExpiresOnUtc.ToString("yyyy-MM-dd");

            return new RenderedNotification(
                "Your rewrite credits expire soon",
                $"""
                Hi {name},

                {credits} Reply In My Voice credit(s) expire on {expiresOn}.

                You can use them from your workspace. If you have questions, contact {supportEmail}.
                """,
                $"""
                <p>Hi {Html(name)},</p>
                <p>{credits} Reply In My Voice credit(s) expire on {Html(expiresOn)}.</p>
                <p>You can use them from your workspace. If you have questions, contact <a href="mailto:{HtmlAttribute(supportEmail)}">{Html(supportEmail)}</a>.</p>
                """);
        });

    public static readonly NotificationTemplate<RefundRequestReceivedNotificationModel> RefundRequestReceived = new(
        "refund-request-received",
        model =>
        {
            var name = SafeName(model.CustomerName);
            var supportEmail = SafeEmail(model.SupportEmail);
            var requestReference = string.IsNullOrWhiteSpace(model.RequestReference)
                ? "your request"
                : model.RequestReference.Trim();

            return new RenderedNotification(
                "We received your refund request",
                $"""
                Hi {name},

                We received {requestReference} and will review it against the Reply In My Voice refund policy.

                If you need to add anything, reply to {supportEmail}.
                """,
                $"""
                <p>Hi {Html(name)},</p>
                <p>We received {Html(requestReference)} and will review it against the Reply In My Voice refund policy.</p>
                <p>If you need to add anything, reply to <a href="mailto:{HtmlAttribute(supportEmail)}">{Html(supportEmail)}</a>.</p>
                """);
        });

    public static readonly NotificationTemplate<GstTurnoverThresholdNotificationModel> GstTurnoverThreshold = new(
        "gst-turnover-threshold",
        model =>
        {
            var supportEmail = SafeEmail(model.SupportEmail);
            var gross = FormatNzd(model.GrossAmountTotal);
            var threshold = FormatNzd(model.RegistrationThresholdAmountTotal);
            var warningPercent = Math.Round(model.WarningFraction * 100m, 0);
            var windowEnd = model.WindowEndUtc.ToString("yyyy-MM-dd");

            return new RenderedNotification(
                "GST turnover threshold warning",
                $"""
                Reply In My Voice gross paid revenue for the rolling 12-month window ending {windowEnd} is {gross}.

                This is at or above the configured {warningPercent}% warning level toward the {threshold} GST registration threshold. Review the admin stats and complete the owner GST checklist before switching Stripe Tax on.

                Support contact: {supportEmail}
                """,
                $"""
                <p>Reply In My Voice gross paid revenue for the rolling 12-month window ending {Html(windowEnd)} is {Html(gross)}.</p>
                <p>This is at or above the configured {Html(warningPercent.ToString("0"))}% warning level toward the {Html(threshold)} GST registration threshold. Review the admin stats and complete the owner GST checklist before switching Stripe Tax on.</p>
                <p>Support contact: <a href="mailto:{HtmlAttribute(supportEmail)}">{Html(supportEmail)}</a></p>
                """);
        });

    private static string SafeName(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "there" : value.Trim();

    private static string SafeEmail(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "info@timeawake.co.nz" : value.Trim();

    private static string SafeUrl(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "https://replyinmyvoice.com/app" : value.Trim();

    private static string FormatNzd(long minorUnits) =>
        $"NZ${(minorUnits / 100m).ToString("0.00", CultureInfo.InvariantCulture)}";

    private static string Html(string value) =>
        WebUtility.HtmlEncode(value);

    private static string HtmlAttribute(string value) =>
        Html(value).Replace("\"", "&quot;", StringComparison.Ordinal);
}

public sealed record FailedPaymentNotificationModel(
    string CustomerName,
    string SupportEmail,
    string BillingPortalUrl);

public sealed record CreditExpiringNotificationModel(
    string CustomerName,
    string SupportEmail,
    int CreditsExpiring,
    DateTimeOffset ExpiresOnUtc);

public sealed record RefundRequestReceivedNotificationModel(
    string CustomerName,
    string SupportEmail,
    string RequestReference);

public sealed record GstTurnoverThresholdNotificationModel(
    long GrossAmountTotal,
    long RegistrationThresholdAmountTotal,
    decimal WarningFraction,
    DateTimeOffset WindowEndUtc,
    string? SupportEmail);
