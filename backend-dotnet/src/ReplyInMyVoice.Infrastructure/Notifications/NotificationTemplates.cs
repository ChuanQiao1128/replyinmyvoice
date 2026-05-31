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

    public static readonly NotificationTemplate<PaymentReconciliationNotificationModel> PaymentReconciliationDiscrepancy = new(
        "payment-reconciliation-discrepancy",
        model =>
        {
            var windowStart = model.WindowStartUtc.ToString("yyyy-MM-dd HH:mm 'UTC'");
            var windowEnd = model.WindowEndUtc.ToString("yyyy-MM-dd HH:mm 'UTC'");

            return new RenderedNotification(
                "Payment reconciliation needs review",
                $"""
                Stripe reconciliation found {model.DiscrepancyCount} discrepancy row(s) for {windowStart} to {windowEnd}.

                paid-but-no-grant: {model.PaidButNoGrantCount}
                grant-but-no-payment: {model.GrantButNoPaymentCount}
                amount-mismatch: {model.AmountMismatchCount}
                """,
                $"""
                <p>Stripe reconciliation found {model.DiscrepancyCount} discrepancy row(s) for {Html(windowStart)} to {Html(windowEnd)}.</p>
                <ul>
                <li>paid-but-no-grant: {model.PaidButNoGrantCount}</li>
                <li>grant-but-no-payment: {model.GrantButNoPaymentCount}</li>
                <li>amount-mismatch: {model.AmountMismatchCount}</li>
                </ul>
                """);
        });

    private static string SafeName(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "there" : value.Trim();

    private static string SafeEmail(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "info@timeawake.co.nz" : value.Trim();

    private static string SafeUrl(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "https://replyinmyvoice.com/app" : value.Trim();

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

public sealed record PaymentReconciliationNotificationModel(
    DateTimeOffset WindowStartUtc,
    DateTimeOffset WindowEndUtc,
    int DiscrepancyCount,
    int PaidButNoGrantCount,
    int GrantButNoPaymentCount,
    int AmountMismatchCount);
