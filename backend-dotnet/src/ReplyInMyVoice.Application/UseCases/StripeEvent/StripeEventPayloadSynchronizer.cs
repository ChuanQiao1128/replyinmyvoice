using System.Data;
using System.Text.Json;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.Common;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;

namespace ReplyInMyVoice.Application.UseCases.StripeEvent;

public sealed class StripeEventPayloadSynchronizer(
    IAppUserRepository appUsers,
    IRewriteCreditRepository credits,
    IStripeInvoiceRepository invoices,
    IOutboxMessageRepository outboxMessages,
    IAdminUserRepository adminUsers,
    IUnitOfWork unitOfWork)
{
    private const int DefaultPaymentGraceDays = 7;
    private const int PaymentGraceReminderElapsedDays = 5;
    private const int PaymentGraceReminderRemainingDays = 2;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async Task<string?> SyncAsync(
        StripeWebhookPayloadDto payload,
        DateTimeOffset now,
        List<Func<CancellationToken, Task>> postCommitActions,
        CancellationToken ct)
    {
        _ = postCommitActions;
        return await SyncPayloadAsync(payload, now, ct);
    }

    private async Task<string?> SyncPayloadAsync(
        StripeWebhookPayloadDto payload,
        DateTimeOffset now,
        CancellationToken ct)
    {
        if (payload.Type.StartsWith("customer.subscription.", StringComparison.Ordinal))
        {
            return await SyncSubscriptionObjectAsync(payload, now, ct);
        }

        return payload.Type switch
        {
            "checkout.session.completed" =>
                await SyncCheckoutSessionAsync(payload, now, ct),
            "invoice.payment_failed" =>
                await SyncInvoicePaymentFailedAsync(payload, now, ct),
            "invoice.payment_action_required" =>
                await SyncInvoicePaymentActionRequiredAsync(payload, now, ct),
            "invoice.payment_succeeded" =>
                await SyncInvoicePaymentSucceededAsync(payload, now, ct),
            "invoice.paid" or "invoice.finalized" =>
                await SyncStripeInvoiceAsync(payload, now, ct),
            "customer.deleted" =>
                await SyncCustomerDeletedAsync(payload, now, ct),
            "customer.source.expiring" =>
                await SyncCustomerSourceExpiringAsync(payload, now, ct),
            "charge.refunded" =>
                await RevokeRefundedChargeCreditsAsync(payload.Object, ct),
            "charge.dispute.created" or "charge.dispute.closed" =>
                await RevokeDisputedChargeCreditsAsync(payload.Object, ct),
            _ => null,
        };
    }

    private async Task<string?> SyncCheckoutSessionAsync(
        StripeWebhookPayloadDto payload,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var stripeObject = payload.Object;
        var externalAuthUserId = Normalize(stripeObject.ExternalAuthUserId);
        var customerId = Normalize(stripeObject.CustomerId);

        if (string.IsNullOrWhiteSpace(externalAuthUserId) && string.IsNullOrWhiteSpace(customerId))
        {
            return null;
        }

        var user = await appUsers.FindForStripeCheckoutAsync(externalAuthUserId, customerId, ct);
        if (user is null)
        {
            return RequiresCheckoutUser(stripeObject)
                ? $"No matching user for Stripe checkout session customer {customerId ?? "unknown"}."
                : null;
        }

        user.StripeCustomerId = customerId ?? user.StripeCustomerId;
        user.StripeSubscriptionId = Normalize(stripeObject.SubscriptionId) ?? user.StripeSubscriptionId;
        user.UpdatedAt = now;

        var paymentIntentId = Normalize(stripeObject.PaymentIntentId);
        if (IsPaidPaymentSession(stripeObject) &&
            !await credits.ExistsByStripeEventIdAsync(payload.EventId, ct) &&
            (paymentIntentId is null ||
                !(await credits.ListByStripePaymentIntentIdAsync(paymentIntentId, ct))
                .Any(x => x.Source == "PURCHASE")) &&
            ResolveGrantedRewrites(stripeObject) is { } rewrites)
        {
            await credits.AddAsync(new RewriteCredit
            {
                UserId = user.Id,
                Source = "PURCHASE",
                AmountGranted = rewrites,
                OriginalAmountGranted = rewrites,
                AmountConsumed = 0,
                GrantedAt = now,
                ExpiresAt = now.AddDays(90),
                StripeEventId = payload.EventId,
                StripePaymentIntentId = paymentIntentId,
                StripeReceiptUrl = Normalize(stripeObject.ReceiptUrl),
                StripeSku = Normalize(stripeObject.Sku),
                StripeAmountTotal = stripeObject.AmountTotal,
                StripeCurrency = Normalize(stripeObject.Currency),
            }, ct);
        }

        return null;
    }

    private async Task<string?> SyncInvoicePaymentFailedAsync(
        StripeWebhookPayloadDto payload,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var stripeObject = payload.Object;
        var customerId = Normalize(stripeObject.CustomerId);
        var subscriptionId = Normalize(stripeObject.SubscriptionId);
        if (string.IsNullOrWhiteSpace(customerId) && string.IsNullOrWhiteSpace(subscriptionId))
        {
            return null;
        }

        var user = await appUsers.FindByStripeCustomerOrSubscriptionAsync(customerId, subscriptionId, ct);
        if (user is null)
        {
            return $"No matching user for Stripe invoice payment_failed customer {customerId ?? "unknown"} subscription {subscriptionId ?? "unknown"}.";
        }

        var invoiceSyncFailure = await UpsertStripeInvoiceAsync(
            user,
            "invoice.payment_failed",
            stripeObject,
            now,
            ct);
        if (invoiceSyncFailure is not null)
        {
            return invoiceSyncFailure;
        }

        user.StripeCustomerId = customerId ?? user.StripeCustomerId;
        user.StripeSubscriptionId = subscriptionId ?? user.StripeSubscriptionId;
        user.SubscriptionStatus = SubscriptionStatus.PastDue;
        user.PaymentFailedAt = now;
        user.PaymentGraceEndsAt = ResolvePaymentGraceEndsAt(stripeObject, user.CurrentPeriodEnd, now);
        user.PaymentGraceReminderSentAt = null;
        user.UpdatedAt = now;
        await outboxMessages.AddAsync(
            StripeNotificationOutboxMessageFactory.Create(
                StripeNotificationOutboxMessageTypes.PaymentFailed,
                user.Id,
                now,
                payload.EventId),
            ct);

        return null;
    }

    private async Task<string?> SyncInvoicePaymentActionRequiredAsync(
        StripeWebhookPayloadDto payload,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var stripeObject = payload.Object;
        var customerId = Normalize(stripeObject.CustomerId);
        var subscriptionId = Normalize(stripeObject.SubscriptionId);
        if (string.IsNullOrWhiteSpace(customerId) && string.IsNullOrWhiteSpace(subscriptionId))
        {
            return null;
        }

        var user = await appUsers.FindByStripeCustomerOrSubscriptionAsync(customerId, subscriptionId, ct);
        if (user is null)
        {
            return $"No matching user for Stripe invoice payment_action_required customer {customerId ?? "unknown"} subscription {subscriptionId ?? "unknown"}.";
        }

        var invoiceSyncFailure = await UpsertStripeInvoiceAsync(
            user,
            "invoice.payment_action_required",
            stripeObject,
            now,
            ct);
        if (invoiceSyncFailure is not null)
        {
            return invoiceSyncFailure;
        }

        user.StripeCustomerId = customerId ?? user.StripeCustomerId;
        user.StripeSubscriptionId = subscriptionId ?? user.StripeSubscriptionId;
        if (subscriptionId is not null &&
            !string.Equals(Normalize(stripeObject.BillingReason), "subscription_create", StringComparison.OrdinalIgnoreCase))
        {
            user.SubscriptionStatus = SubscriptionStatus.PastDue;
            user.PaymentFailedAt ??= now;
            user.PaymentGraceEndsAt ??= ResolvePaymentGraceEndsAt(stripeObject, user.CurrentPeriodEnd, now);
        }

        user.UpdatedAt = now;
        await outboxMessages.AddAsync(
            StripeNotificationOutboxMessageFactory.CreatePaymentActionRequired(
                user.Id,
                Normalize(stripeObject.Id),
                Normalize(stripeObject.HostedInvoiceUrl),
                now,
                payload.EventId),
            ct);

        return null;
    }

    private async Task<string?> SyncInvoicePaymentSucceededAsync(
        StripeWebhookPayloadDto payload,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var stripeObject = payload.Object;
        var customerId = Normalize(stripeObject.CustomerId);
        var subscriptionId = Normalize(stripeObject.SubscriptionId);
        if (string.IsNullOrWhiteSpace(customerId) && string.IsNullOrWhiteSpace(subscriptionId))
        {
            return null;
        }

        var user = await appUsers.FindByStripeCustomerOrSubscriptionAsync(customerId, subscriptionId, ct);
        if (user is null)
        {
            return $"No matching user for Stripe invoice payment_succeeded customer {customerId ?? "unknown"} subscription {subscriptionId ?? "unknown"}.";
        }

        var invoiceSyncFailure = await UpsertStripeInvoiceAsync(
            user,
            "invoice.payment_succeeded",
            stripeObject,
            now,
            ct);
        if (invoiceSyncFailure is not null)
        {
            return invoiceSyncFailure;
        }

        if (!HasPaymentGrace(user))
        {
            return null;
        }

        var recoveredToActive = user.SubscriptionStatus == SubscriptionStatus.PastDue;
        if (recoveredToActive)
        {
            user.SubscriptionStatus = SubscriptionStatus.Active;
        }

        ClearPaymentGrace(user);
        user.UpdatedAt = now;
        if (recoveredToActive)
        {
            await outboxMessages.AddAsync(
                StripeNotificationOutboxMessageFactory.Create(
                    StripeNotificationOutboxMessageTypes.PaymentRecovered,
                    user.Id,
                    now,
                    payload.EventId),
                ct);
        }

        return null;
    }

    private async Task<string?> SyncStripeInvoiceAsync(
        StripeWebhookPayloadDto payload,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var customerId = Normalize(payload.Object.CustomerId);
        var subscriptionId = Normalize(payload.Object.SubscriptionId);
        if (string.IsNullOrWhiteSpace(customerId) && string.IsNullOrWhiteSpace(subscriptionId))
        {
            return null;
        }

        var user = await appUsers.FindByStripeCustomerOrSubscriptionAsync(customerId, subscriptionId, ct);
        if (user is null)
        {
            return $"No matching user for Stripe {payload.Type} customer {customerId ?? "unknown"} subscription {subscriptionId ?? "unknown"}.";
        }

        return await UpsertStripeInvoiceAsync(user, payload.Type, payload.Object, now, ct);
    }

    private async Task<string?> SyncCustomerDeletedAsync(
        StripeWebhookPayloadDto payload,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var customerId = Normalize(payload.Object.Id);
        if (customerId is null)
        {
            return null;
        }

        var user = await appUsers.GetByStripeCustomerIdAsync(customerId, ct);
        if (user is null)
        {
            return null;
        }

        var previousCustomerId = user.StripeCustomerId;
        var previousSubscriptionId = user.StripeSubscriptionId;
        var previousStatus = user.SubscriptionStatus;

        user.StripeCustomerId = null;
        user.StripeSubscriptionId = null;
        user.SubscriptionStatus = SubscriptionStatus.Inactive;
        ClearPaymentGrace(user);
        user.UpdatedAt = now;

        await adminUsers.AddAuditLogAsync(new AdminAuditLog
        {
            AdminExternalAuthUserId = "system:stripe-webhook",
            AdminEmail = "system@replyinmyvoice.com",
            Action = "stripe.customer.deleted",
            TargetUserId = user.Id,
            DetailsJson = JsonSerializer.Serialize(
                new
                {
                    stripeEventId = payload.EventId,
                    stripeCustomerId = previousCustomerId,
                    stripeSubscriptionId = previousSubscriptionId,
                    previousSubscriptionStatus = previousStatus.ToString(),
                },
                JsonOptions),
            CreatedAt = now,
        }, ct);

        return null;
    }

    private async Task<string?> SyncCustomerSourceExpiringAsync(
        StripeWebhookPayloadDto payload,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var stripeObject = payload.Object;
        var customerId = Normalize(stripeObject.CustomerId);
        if (customerId is null)
        {
            return null;
        }

        var user = await appUsers.GetByStripeCustomerIdAsync(customerId, ct);
        if (user is null ||
            user.SubscriptionStatus is not (SubscriptionStatus.Active or SubscriptionStatus.Trialing or SubscriptionStatus.PastDue))
        {
            return null;
        }

        await outboxMessages.AddAsync(
            StripeNotificationOutboxMessageFactory.CreateCardExpiring(
                user.Id,
                Normalize(stripeObject.CardBrand),
                Normalize(stripeObject.CardLast4),
                stripeObject.CardExpMonth,
                stripeObject.CardExpYear,
                now,
                payload.EventId),
            ct);

        return null;
    }

    private async Task<string?> SyncSubscriptionObjectAsync(
        StripeWebhookPayloadDto payload,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var stripeObject = payload.Object;
        var customerId = Normalize(stripeObject.CustomerId);
        if (string.IsNullOrWhiteSpace(customerId))
        {
            return null;
        }

        var user = await appUsers.GetByStripeCustomerIdAsync(customerId, ct);
        if (user is null)
        {
            return $"No matching user for Stripe subscription customer {customerId}.";
        }

        var rawStatus = payload.Type == "customer.subscription.deleted"
            ? "canceled"
            : Normalize(stripeObject.Status);
        var status = payload.Type == "customer.subscription.deleted"
            ? SubscriptionStatus.Canceled
            : MapSubscriptionStatus(rawStatus);
        var wasInPaymentGrace = HasPaymentGrace(user) || user.SubscriptionStatus == SubscriptionStatus.PastDue;

        user.StripeSubscriptionId = Normalize(stripeObject.Id) ?? user.StripeSubscriptionId;
        user.SubscriptionStatus = status;
        user.CurrentPeriodEnd = stripeObject.CurrentPeriodEnd;
        user.UpdatedAt = now;

        if (status == SubscriptionStatus.PastDue)
        {
            user.PaymentFailedAt ??= now;
            user.PaymentGraceEndsAt ??= ResolvePaymentGraceEndsAt(stripeObject, user.CurrentPeriodEnd, now);
        }
        else if (IsTerminalDunningStatus(rawStatus))
        {
            if (wasInPaymentGrace)
            {
                await outboxMessages.AddAsync(
                    StripeNotificationOutboxMessageFactory.Create(
                        StripeNotificationOutboxMessageTypes.SubscriptionPaused,
                        user.Id,
                        now,
                        payload.EventId),
                    ct);
            }

            ClearPaymentGrace(user);
        }
        else if (status is SubscriptionStatus.Active or SubscriptionStatus.Trialing or SubscriptionStatus.Testing)
        {
            ClearPaymentGrace(user);
        }

        return null;
    }

    private async Task<string?> UpsertStripeInvoiceAsync(
        AppUser user,
        string type,
        StripeWebhookObjectDto stripeObject,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var invoiceId = Normalize(stripeObject.Id);
        if (string.IsNullOrWhiteSpace(invoiceId))
        {
            return "Stripe invoice event missing invoice id.";
        }

        var invoice = await invoices.GetByIdAsync(invoiceId, ct);
        if (invoice is null)
        {
            await invoices.AddAsync(new StripeInvoice
            {
                Id = invoiceId,
                UserId = user.Id,
                Status = ResolveStripeInvoiceStatus(stripeObject, type),
                AmountDue = stripeObject.AmountDue ?? 0,
                AmountPaid = stripeObject.AmountPaid ?? 0,
                Currency = Normalize(stripeObject.Currency) ?? string.Empty,
                SubscriptionId = Normalize(stripeObject.SubscriptionId),
                PeriodStart = stripeObject.PeriodStart,
                PeriodEnd = stripeObject.PeriodEnd,
                AttemptCount = stripeObject.AttemptCount,
                NextPaymentAttempt = stripeObject.NextPaymentAttempt,
                HostedInvoiceUrl = Normalize(stripeObject.HostedInvoiceUrl),
                InvoicePdf = Normalize(stripeObject.InvoicePdf),
                CreatedAt = now,
                UpdatedAt = now,
            }, ct);
            return null;
        }

        invoice.UserId = user.Id;
        invoice.SubscriptionId = Normalize(stripeObject.SubscriptionId);
        invoice.Status = ResolveStripeInvoiceStatus(stripeObject, type);
        invoice.AmountDue = stripeObject.AmountDue ?? 0;
        invoice.AmountPaid = stripeObject.AmountPaid ?? 0;
        invoice.Currency = Normalize(stripeObject.Currency) ?? string.Empty;
        invoice.PeriodStart = stripeObject.PeriodStart;
        invoice.PeriodEnd = stripeObject.PeriodEnd;
        invoice.AttemptCount = stripeObject.AttemptCount;
        invoice.NextPaymentAttempt = stripeObject.NextPaymentAttempt;
        invoice.HostedInvoiceUrl = Normalize(stripeObject.HostedInvoiceUrl);
        invoice.InvoicePdf = Normalize(stripeObject.InvoicePdf);
        invoice.UpdatedAt = now;
        return null;
    }

    private async Task<string?> RevokeRefundedChargeCreditsAsync(
        StripeWebhookObjectDto stripeObject,
        CancellationToken ct)
    {
        var paymentIntentId = Normalize(stripeObject.PaymentIntentId);
        if (string.IsNullOrWhiteSpace(paymentIntentId))
        {
            return null;
        }

        var matchingCredits = await credits.ListByStripePaymentIntentIdAsync(paymentIntentId, ct);
        if (matchingCredits.Count == 0)
        {
            return $"No matching rewrite credit for Stripe refund payment_intent {paymentIntentId}.";
        }

        foreach (var credit in matchingCredits)
        {
            credit.OriginalAmountGranted ??= credit.AmountGranted;

            if (IsFullRefund(stripeObject))
            {
                credit.AmountGranted = credit.AmountConsumed;
            }
            else if (ResolveRemainingGrantedAfterRefund(stripeObject, credit) is { } targetGranted)
            {
                credit.AmountGranted = targetGranted;
            }
        }

        return null;
    }

    private async Task<string?> RevokeDisputedChargeCreditsAsync(
        StripeWebhookObjectDto stripeObject,
        CancellationToken ct)
    {
        var paymentIntentId = Normalize(stripeObject.PaymentIntentId);
        if (string.IsNullOrWhiteSpace(paymentIntentId))
        {
            return null;
        }

        var matchingCredits = await credits.ListByStripePaymentIntentIdAsync(paymentIntentId, ct);
        foreach (var credit in matchingCredits)
        {
            if (credit.AmountGranted == credit.AmountConsumed)
            {
                continue;
            }

            credit.AmountGranted = credit.AmountConsumed;
        }

        return null;
    }

    private static bool HasPaymentGrace(AppUser user) =>
        user.PaymentFailedAt is not null || user.PaymentGraceEndsAt is not null;

    private static void ClearPaymentGrace(AppUser user)
    {
        user.PaymentFailedAt = null;
        user.PaymentGraceEndsAt = null;
        user.PaymentGraceReminderSentAt = null;
    }

    private static DateTimeOffset ResolvePaymentGraceEndsAt(
        StripeWebhookObjectDto stripeObject,
        DateTimeOffset? currentPeriodEnd,
        DateTimeOffset now)
    {
        var graceEndsAt = stripeObject.NextPaymentAttempt ??
            stripeObject.DueDate ??
            stripeObject.CurrentPeriodEnd ??
            currentPeriodEnd;

        if (graceEndsAt is { } candidate && candidate > now)
        {
            return candidate;
        }

        return now.AddDays(DefaultPaymentGraceDays);
    }

    private static bool ShouldSendPaymentGraceReminder(AppUser user, DateTimeOffset now)
    {
        if (user.PaymentGraceEndsAt is null || user.PaymentGraceEndsAt <= now)
        {
            return false;
        }

        var reminderAt = user.PaymentFailedAt is { } failedAt && failedAt < user.PaymentGraceEndsAt
            ? failedAt.AddDays(PaymentGraceReminderElapsedDays)
            : user.PaymentGraceEndsAt.Value.AddDays(-PaymentGraceReminderRemainingDays);

        return reminderAt <= now;
    }

    private static bool IsTerminalDunningStatus(string? status)
    {
        var normalized = status?.Trim().ToLowerInvariant();
        return normalized is "unpaid" or "canceled";
    }

    private static SubscriptionStatus MapSubscriptionStatus(string? status)
    {
        return status?.Trim().ToLowerInvariant() switch
        {
            "active" => SubscriptionStatus.Active,
            "trialing" => SubscriptionStatus.Trialing,
            "past_due" => SubscriptionStatus.PastDue,
            "canceled" => SubscriptionStatus.Inactive,
            "unpaid" or
            "incomplete" or
            "incomplete_expired" or
            "paused" => SubscriptionStatus.Inactive,
            _ => SubscriptionStatus.Inactive,
        };
    }

    private static bool IsPaidPaymentSession(StripeWebhookObjectDto stripeObject) =>
        Normalize(stripeObject.CheckoutMode) == "payment" &&
        Normalize(stripeObject.PaymentStatus) == "paid";

    private static bool RequiresCheckoutUser(StripeWebhookObjectDto stripeObject) =>
        IsPaidPaymentSession(stripeObject) ||
        Normalize(stripeObject.CheckoutMode) == "subscription" ||
        !string.IsNullOrWhiteSpace(stripeObject.SubscriptionId);

    private static int? ResolveGrantedRewrites(StripeWebhookObjectDto stripeObject)
    {
        if (stripeObject.GrantedRewrites is > 0)
        {
            return stripeObject.GrantedRewrites;
        }

        return Normalize(stripeObject.Sku) switch
        {
            "quick_pack" => 10,
            "value_pack" => 30,
            _ => null,
        };
    }

    private static bool IsFullRefund(StripeWebhookObjectDto stripeObject)
    {
        if (stripeObject.Refunded == true)
        {
            return true;
        }

        return stripeObject.Amount is > 0 &&
            stripeObject.AmountRefunded >= stripeObject.Amount;
    }

    private static int? ResolveRemainingGrantedAfterRefund(
        StripeWebhookObjectDto stripeObject,
        RewriteCredit credit)
    {
        var amount = stripeObject.Amount ?? credit.StripeAmountTotal;
        var amountRefunded = stripeObject.AmountRefunded;
        var originalGranted = ResolveOriginalGrantedRewrites(credit);
        if (amount is not > 0 || amountRefunded is not > 0 || originalGranted is not > 0)
        {
            return null;
        }

        var boundedRefundedAmount = Math.Min(amountRefunded.Value, amount.Value);
        var refundedCredits = (int)Math.Ceiling((decimal)originalGranted.Value * boundedRefundedAmount / amount.Value);
        return Math.Max(credit.AmountConsumed, originalGranted.Value - refundedCredits);
    }

    private static int? ResolveOriginalGrantedRewrites(RewriteCredit credit)
    {
        if (credit.OriginalAmountGranted is > 0)
        {
            return credit.OriginalAmountGranted;
        }

        return Normalize(credit.StripeSku) switch
        {
            "quick_pack" => 10,
            "value_pack" => 30,
            _ => null,
        };
    }

    private static string ResolveStripeInvoiceStatus(
        StripeWebhookObjectDto stripeObject,
        string type)
    {
        var status = Normalize(stripeObject.Status);
        if (!string.IsNullOrWhiteSpace(status))
        {
            return status;
        }

        return type switch
        {
            "invoice.paid" or "invoice.payment_succeeded" => "paid",
            "invoice.payment_failed" or "invoice.finalized" => "open",
            _ => "open",
        };
    }

    private static string? Normalize(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }
}
