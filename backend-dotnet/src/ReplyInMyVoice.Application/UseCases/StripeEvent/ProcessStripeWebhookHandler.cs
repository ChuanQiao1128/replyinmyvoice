using System.Data;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.Common;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;

namespace ReplyInMyVoice.Application.UseCases.StripeEvent;

public sealed class ProcessStripeWebhookHandler(
    IStripeEventRepository stripeEvents,
    IAppUserRepository appUsers,
    IRewriteCreditRepository credits,
    IStripeInvoiceRepository invoices,
    IOutboxMessageRepository outboxMessages,
    IUnitOfWork unitOfWork,
    IBusinessMetrics? metrics = null)
{
    private const int DefaultPaymentGraceDays = 7;
    private const int PaymentGraceReminderElapsedDays = 5;
    private const int PaymentGraceReminderRemainingDays = 2;
    private readonly IBusinessMetrics _metrics = metrics ?? NoOpBusinessMetrics.Instance;

    public async Task<bool> HandleAsync(
        ProcessStripeWebhookCommand command,
        CancellationToken ct = default)
    {
        try
        {
            string? syncFailureForMetrics = null;
            var processed = await unitOfWork.ExecuteInTransactionAsync(
                async transactionCt =>
                {
                    syncFailureForMetrics = null;
                    var stripeEvent = await stripeEvents.BeginProcessingAsync(
                        command.Payload.EventId,
                        command.Payload.Type,
                        command.Now,
                        transactionCt);
                    if (stripeEvent is null)
                    {
                        return false;
                    }

                    var syncFailure = await SyncPayloadAsync(
                        command.Payload,
                        command.Now,
                        transactionCt);
                    if (syncFailure is not null)
                    {
                        syncFailureForMetrics = syncFailure;
                        stripeEvents.MarkFailed(stripeEvent, syncFailure, command.Now);
                        await unitOfWork.SaveChangesAsync(transactionCt);
                        return false;
                    }

                    stripeEvents.MarkProcessed(stripeEvent, command.Now);
                    await unitOfWork.SaveChangesAsync(transactionCt);
                    return true;
                },
                IsolationLevel.Serializable,
                ct);

            if (syncFailureForMetrics is not null)
            {
                RecordFailedMetric(command.Payload.Type);
            }

            if (processed && command.Payload.EventCreatedAt is { } eventCreatedAt)
            {
                _metrics.Record(
                    BusinessMetricNames.WebhookProcessingLagSeconds,
                    Math.Max(0, (command.Now - eventCreatedAt).TotalSeconds));
            }

            return processed;
        }
        catch (Exception ex)
            when (command.Payload.Type == "checkout.session.completed" &&
                credits.IsStripeEventIdWriteFailure(ex))
        {
            await MarkProcessedAfterCheckoutGrantConflictAsync(command.Payload, command.Now, ct);
            return false;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            RecordFailedMetric(command.Payload.Type);
            await MarkFailedAsync(command.Payload, ex.Message, command.Now, ct);
            throw;
        }
    }

    private void RecordFailedMetric(string eventType) =>
        _metrics.Record(
            BusinessMetricNames.StripeEventFailedTotal,
            1,
            BusinessMetricDimensions.EventType,
            eventType);

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
            "invoice.payment_succeeded" =>
                await SyncInvoicePaymentSucceededAsync(payload, now, ct),
            "invoice.paid" or "invoice.finalized" =>
                await SyncStripeInvoiceAsync(payload, now, ct),
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
        user.RowVersion = Guid.NewGuid();

        if (IsPaidPaymentSession(stripeObject) &&
            !await credits.ExistsByStripeEventIdAsync(payload.EventId, ct) &&
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
                StripePaymentIntentId = Normalize(stripeObject.PaymentIntentId),
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
        user.RowVersion = Guid.NewGuid();
        await outboxMessages.AddAsync(
            StripeNotificationOutboxMessageFactory.Create(
                StripeNotificationOutboxMessageTypes.PaymentFailed,
                user.Id,
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
        user.RowVersion = Guid.NewGuid();
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
        user.RowVersion = Guid.NewGuid();

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
        invoice.RowVersion = Guid.NewGuid();
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
            var previousGranted = credit.AmountGranted;
            var previousOriginalGranted = credit.OriginalAmountGranted;
            credit.OriginalAmountGranted ??= credit.AmountGranted;

            if (IsFullRefund(stripeObject))
            {
                credit.AmountGranted = credit.AmountConsumed;
            }
            else if (ResolveRemainingGrantedAfterRefund(stripeObject, credit) is { } targetGranted)
            {
                credit.AmountGranted = targetGranted;
            }

            if (credit.AmountGranted != previousGranted ||
                credit.OriginalAmountGranted != previousOriginalGranted)
            {
                credit.RowVersion = Guid.NewGuid();
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
            credit.RowVersion = Guid.NewGuid();
        }

        return null;
    }

    private async Task MarkProcessedAfterCheckoutGrantConflictAsync(
        StripeWebhookPayloadDto payload,
        DateTimeOffset now,
        CancellationToken ct)
    {
        await unitOfWork.ExecuteInTransactionAsync(
            async transactionCt =>
            {
                var stripeEvent = await stripeEvents.GetByEventIdAsync(payload.EventId, transactionCt);
                if (stripeEvent is null)
                {
                    await stripeEvents.AddAsync(new Domain.Entities.StripeEvent
                    {
                        EventId = payload.EventId,
                        Type = payload.Type,
                        Status = StripeEventStatus.Processed,
                        AttemptCount = 1,
                        CreatedAt = now,
                        LastAttemptAt = now,
                        ProcessedAt = now,
                    }, transactionCt);
                }
                else if (stripeEvent.Status != StripeEventStatus.Processed)
                {
                    stripeEvent.Type = payload.Type;
                    stripeEvent.AttemptCount += 1;
                    stripeEvent.LastAttemptAt = now;
                    stripeEvents.MarkProcessed(stripeEvent, now);
                }

                await unitOfWork.SaveChangesAsync(transactionCt);
            },
            IsolationLevel.Serializable,
            ct);
    }

    private async Task MarkFailedAsync(
        StripeWebhookPayloadDto payload,
        string error,
        DateTimeOffset now,
        CancellationToken ct)
    {
        await unitOfWork.ExecuteInTransactionAsync(
            async transactionCt =>
            {
                var stripeEvent = await stripeEvents.GetByEventIdAsync(payload.EventId, transactionCt);
                if (stripeEvent?.Status == StripeEventStatus.Processed)
                {
                    return;
                }

                var truncatedError = TruncateStripeEventError(error);
                if (stripeEvent is null)
                {
                    await stripeEvents.AddAsync(new Domain.Entities.StripeEvent
                    {
                        EventId = payload.EventId,
                        Type = payload.Type,
                        Status = StripeEventStatus.Failed,
                        AttemptCount = 1,
                        CreatedAt = now,
                        LastAttemptAt = now,
                        LastError = truncatedError,
                    }, transactionCt);
                }
                else
                {
                    stripeEvent.Type = payload.Type;
                    stripeEvent.AttemptCount += 1;
                    stripeEvents.MarkFailed(stripeEvent, truncatedError, now);
                }

                await unitOfWork.SaveChangesAsync(transactionCt);
            },
            IsolationLevel.Serializable,
            ct);
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

    private static string TruncateStripeEventError(string error) =>
        error.Length > 1000 ? error[..1000] : error;

    private static string? Normalize(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }
}
