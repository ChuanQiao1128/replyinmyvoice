using System.Text.Json;
using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Infrastructure.Data;
using ReplyInMyVoice.Infrastructure.Notifications;

namespace ReplyInMyVoice.Infrastructure.Services;

public sealed class StripeEventService
{
    private const int DefaultPaymentGraceDays = 7;
    private const string SupportEmail = "info@timeawake.co.nz";

    private readonly Func<AppDbContext> dbContextFactory;
    private readonly INotificationService? notificationService;
    private readonly IStripeBillingService? stripeBillingService;
    private readonly ILogger<StripeEventService>? logger;

    public StripeEventService(
        Func<AppDbContext> dbContextFactory,
        ILogger<StripeEventService>? logger = null)
        : this(dbContextFactory, null, null, logger)
    {
    }

    public StripeEventService(
        Func<AppDbContext> dbContextFactory,
        INotificationService? notificationService,
        IStripeBillingService? stripeBillingService = null,
        ILogger<StripeEventService>? logger = null)
    {
        StripeBillingService.EnsureStripeApiVersionPinned();
        this.dbContextFactory = dbContextFactory;
        this.notificationService = notificationService;
        this.stripeBillingService = stripeBillingService;
        this.logger = logger;
    }

    public async Task<bool> TryMarkProcessedAsync(
        string eventId,
        string type,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        using var scope = BeginEventScope(eventId);
        await using var db = dbContextFactory();

        db.StripeEvents.Add(new StripeEvent
        {
            EventId = eventId,
            Type = type,
            Status = StripeEventStatus.Processed,
            AttemptCount = 1,
            CreatedAt = now,
            LastAttemptAt = now,
            ProcessedAt = now,
        });

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            logger?.LogInformation(
                "Stripe webhook event marked processed for event {EventId} of type {EventType}.",
                eventId,
                type);
            return true;
        }
        catch (DbUpdateException ex)
        {
            logger?.LogInformation(
                ex,
                "Stripe webhook event already exists for event {EventId} of type {EventType}; skipping duplicate processed mark.",
                eventId,
                type);
            return false;
        }
    }

    public async Task<bool> ProcessWebhookEventAsync(
        string eventId,
        string type,
        string rawBody,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        using var scope = BeginEventScope(eventId);
        var postCommitActions = new List<Func<CancellationToken, Task>>();
        try
        {
            var processed = await ExecuteInTransactionAsync(async db =>
            {
                var stripeEvent = await TryBeginProcessingAsync(db, eventId, type, now, cancellationToken);
                if (stripeEvent is null)
                {
                    return false;
                }

                var syncFailure = await SyncEntitlementAsync(
                    db,
                    eventId,
                    type,
                    rawBody,
                    now,
                    postCommitActions,
                    cancellationToken);
                if (syncFailure is not null)
                {
                    MarkFailed(stripeEvent, syncFailure, now);
                    await db.SaveChangesAsync(cancellationToken);
                    logger?.LogError(
                        "{PaymentObservabilityEvent} for correlation {CorrelationId}, Stripe event {EventId} of type {EventType}, attempt {AttemptCount}: {WebhookFailureReason}",
                        "webhook_failed",
                        eventId,
                        eventId,
                        type,
                        stripeEvent.AttemptCount,
                        syncFailure);
                    return false;
                }

                MarkProcessed(stripeEvent, now);
                await db.SaveChangesAsync(cancellationToken);
                return true;
            }, cancellationToken);

            if (processed)
            {
                logger?.LogInformation(
                    "Stripe webhook processed for event {EventId} of type {EventType}.",
                    eventId,
                    type);
                await RunPostCommitActionsAsync(postCommitActions, cancellationToken);
            }
            else
            {
                logger?.LogInformation(
                    "Stripe webhook skipped for event {EventId} of type {EventType} because it is already processed, locked, or awaiting retry.",
                    eventId,
                    type);
            }

            return processed;
        }
        catch (DbUpdateException ex)
            when (type == "checkout.session.completed" && IsStripeEventCreditUniqueConstraintViolation(ex))
        {
            await MarkProcessedAfterCheckoutGrantConflictAsync(eventId, type, now, cancellationToken);
            logger?.LogInformation(
                ex,
                "Stripe checkout credit grant already exists for event {EventId}; treating webhook as an idempotent success.",
                eventId);
            return false;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var failure = await MarkFailedAsync(eventId, type, ex.Message, now, cancellationToken);
            logger?.LogError(
                ex,
                "{PaymentObservabilityEvent} for correlation {CorrelationId}, Stripe webhook failed for event {EventId} of type {EventType} after {AttemptCount} attempt(s).",
                "webhook_failed",
                eventId,
                eventId,
                type,
                failure?.AttemptCount ?? 0);
            throw;
        }
    }

    public async Task<int> ProcessExpiredPaymentGraceAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var postCommitActions = new List<Func<CancellationToken, Task>>();
        var processedCount = await ExecuteInTransactionAsync(async db =>
        {
            var graceUsers = await db.AppUsers
                .AsTracking()
                .Where(x => x.SubscriptionStatus == SubscriptionStatus.PastDue &&
                    x.PaymentGraceEndsAt != null)
                .ToListAsync(cancellationToken);
            var expiredUsers = graceUsers
                .Where(x => x.PaymentGraceEndsAt <= now)
                .ToList();

            foreach (var user in expiredUsers)
            {
                EnqueueCancelStripeSubscription(postCommitActions, user);
                user.SubscriptionStatus = SubscriptionStatus.Inactive;
                ClearPaymentGrace(user);
                user.UpdatedAt = now;
                user.RowVersion = Guid.NewGuid();
                EnqueueSubscriptionPausedNotification(postCommitActions, user);
            }

            await db.SaveChangesAsync(cancellationToken);
            return expiredUsers.Count;
        }, cancellationToken);

        if (processedCount > 0)
        {
            await RunPostCommitActionsAsync(postCommitActions, cancellationToken);
        }

        return processedCount;
    }

    private IDisposable? BeginEventScope(string eventId) =>
        logger?.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = eventId,
            ["eventId"] = eventId,
        });

    private async Task<StripeEvent?> TryBeginProcessingAsync(
        AppDbContext db,
        string eventId,
        string type,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var stripeEvent = await db.StripeEvents
            .AsTracking()
            .SingleOrDefaultAsync(x => x.EventId == eventId, cancellationToken);

        if (stripeEvent is null)
        {
            stripeEvent = new StripeEvent
            {
                EventId = eventId,
                Type = type,
                Status = StripeEventStatus.Processing,
                AttemptCount = 1,
                CreatedAt = now,
                LastAttemptAt = now,
                LockedUntil = now.AddMinutes(2),
            };
            db.StripeEvents.Add(stripeEvent);
            return stripeEvent;
        }

        if (stripeEvent.Status == StripeEventStatus.Processed)
        {
            return null;
        }

        if (stripeEvent.Status == StripeEventStatus.Processing && stripeEvent.LockedUntil > now)
        {
            return null;
        }

        stripeEvent.Type = type;
        stripeEvent.Status = StripeEventStatus.Processing;
        stripeEvent.AttemptCount += 1;
        stripeEvent.LastAttemptAt = now;
        stripeEvent.LockedUntil = now.AddMinutes(2);
        stripeEvent.LastError = null;
        stripeEvent.RowVersion = Guid.NewGuid();

        return stripeEvent;
    }

    private async Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<AppDbContext, Task<TResult>> operation,
        CancellationToken cancellationToken)
    {
        await using var strategyDb = dbContextFactory();
        var strategy = strategyDb.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            await using var db = dbContextFactory();
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

            var result = await operation(db);

            await transaction.CommitAsync(cancellationToken);
            return result;
        });
    }

    private static void MarkProcessed(StripeEvent stripeEvent, DateTimeOffset now)
    {
        stripeEvent.Status = StripeEventStatus.Processed;
        stripeEvent.ProcessedAt = now;
        stripeEvent.LockedUntil = null;
        stripeEvent.LastError = null;
        stripeEvent.RowVersion = Guid.NewGuid();
    }

    private static void MarkFailed(StripeEvent stripeEvent, string error, DateTimeOffset now)
    {
        stripeEvent.Status = StripeEventStatus.Failed;
        stripeEvent.ProcessedAt = null;
        stripeEvent.LockedUntil = null;
        stripeEvent.LastAttemptAt = now;
        stripeEvent.LastError = TruncateStripeEventError(error);
        stripeEvent.RowVersion = Guid.NewGuid();
    }

    private async Task<StripeFailureLogInfo?> MarkFailedAsync(
        string eventId,
        string type,
        string error,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        return await ExecuteInTransactionAsync(async db =>
        {
            var stripeEvent = await db.StripeEvents
                .AsTracking()
                .SingleOrDefaultAsync(x => x.EventId == eventId, cancellationToken);

            if (stripeEvent?.Status == StripeEventStatus.Processed)
            {
                return new StripeFailureLogInfo(stripeEvent.AttemptCount, stripeEvent.Status);
            }

            var truncatedError = error.Length > 1000 ? error[..1000] : error;
            if (stripeEvent is null)
            {
                stripeEvent = new StripeEvent
                {
                    EventId = eventId,
                    Type = type,
                    Status = StripeEventStatus.Failed,
                    AttemptCount = 1,
                    CreatedAt = now,
                    LastAttemptAt = now,
                    LastError = truncatedError,
                };
                db.StripeEvents.Add(stripeEvent);
            }
            else
            {
                stripeEvent.Type = type;
                stripeEvent.AttemptCount += 1;
                MarkFailed(stripeEvent, truncatedError, now);
            }

            await db.SaveChangesAsync(cancellationToken);
            return new StripeFailureLogInfo(stripeEvent.AttemptCount, stripeEvent.Status);
        }, cancellationToken);
    }

    private async Task MarkProcessedAfterCheckoutGrantConflictAsync(
        string eventId,
        string type,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await ExecuteInTransactionAsync(async db =>
        {
            var stripeEvent = await db.StripeEvents
                .AsTracking()
                .SingleOrDefaultAsync(x => x.EventId == eventId, cancellationToken);

            if (stripeEvent is null)
            {
                stripeEvent = new StripeEvent
                {
                    EventId = eventId,
                    Type = type,
                    Status = StripeEventStatus.Processed,
                    AttemptCount = 1,
                    CreatedAt = now,
                    LastAttemptAt = now,
                    ProcessedAt = now,
                };
                db.StripeEvents.Add(stripeEvent);
            }
            else if (stripeEvent.Status != StripeEventStatus.Processed)
            {
                stripeEvent.Type = type;
                stripeEvent.AttemptCount += 1;
                stripeEvent.LastAttemptAt = now;
                MarkProcessed(stripeEvent, now);
            }

            await db.SaveChangesAsync(cancellationToken);
            return true;
        }, cancellationToken);
    }

    private async Task<string?> SyncEntitlementAsync(
        AppDbContext db,
        string eventId,
        string type,
        string rawBody,
        DateTimeOffset now,
        List<Func<CancellationToken, Task>> postCommitActions,
        CancellationToken cancellationToken)
    {
        using var document = JsonDocument.Parse(rawBody);
        if (!document.RootElement.TryGetProperty("data", out var data) ||
            !data.TryGetProperty("object", out var stripeObject))
        {
            return null;
        }

        if (type.StartsWith("customer.subscription.", StringComparison.Ordinal))
        {
            return await SyncSubscriptionObjectAsync(db, type, stripeObject, now, postCommitActions, cancellationToken);
        }

        if (type == "checkout.session.completed")
        {
            return await SyncCheckoutSessionAsync(db, eventId, stripeObject, now, cancellationToken);
        }

        if (type == "invoice.payment_failed")
        {
            return await SyncInvoicePaymentFailedAsync(db, eventId, stripeObject, now, postCommitActions, cancellationToken);
        }

        if (type == "invoice.payment_succeeded")
        {
            return await SyncInvoicePaymentSucceededAsync(db, stripeObject, now, postCommitActions, cancellationToken);
        }

        if (type is "invoice.paid" or "invoice.finalized")
        {
            return await SyncStripeInvoiceAsync(db, type, stripeObject, now, cancellationToken);
        }

        if (type == "charge.refunded")
        {
            return await RevokeRefundedChargeCreditsAsync(db, stripeObject, cancellationToken);
        }

        if (type is "charge.dispute.created" or "charge.dispute.closed")
        {
            await RevokeDisputedChargeCreditsAsync(db, stripeObject, cancellationToken);
        }

        return null;
    }

    private async Task<string?> SyncInvoicePaymentFailedAsync(
        AppDbContext db,
        string eventId,
        JsonElement stripeObject,
        DateTimeOffset now,
        List<Func<CancellationToken, Task>> postCommitActions,
        CancellationToken cancellationToken)
    {
        var customerId = GetString(stripeObject, "customer");
        var subscriptionId = GetInvoiceSubscriptionId(stripeObject);
        if (string.IsNullOrWhiteSpace(customerId) && string.IsNullOrWhiteSpace(subscriptionId))
        {
            return null;
        }

        var user = await db.AppUsers
            .AsTracking()
            .SingleOrDefaultAsync(
                x => (!string.IsNullOrWhiteSpace(customerId) && x.StripeCustomerId == customerId) ||
                    (!string.IsNullOrWhiteSpace(subscriptionId) && x.StripeSubscriptionId == subscriptionId),
                cancellationToken);
        if (user is null)
        {
            return $"No matching user for Stripe invoice payment_failed customer {customerId ?? "unknown"} subscription {subscriptionId ?? "unknown"}.";
        }

        var invoiceSyncFailure = await UpsertStripeInvoiceAsync(
            db,
            user,
            "invoice.payment_failed",
            stripeObject,
            now,
            cancellationToken);
        if (invoiceSyncFailure is not null)
        {
            return invoiceSyncFailure;
        }

        user.StripeCustomerId = customerId ?? user.StripeCustomerId;
        user.StripeSubscriptionId = subscriptionId ?? user.StripeSubscriptionId;
        user.SubscriptionStatus = SubscriptionStatus.PastDue;
        user.PaymentFailedAt = now;
        user.PaymentGraceEndsAt = ResolvePaymentGraceEndsAt(stripeObject, user.CurrentPeriodEnd, now);
        user.UpdatedAt = now;
        user.RowVersion = Guid.NewGuid();
        EnqueueFailedPaymentNotification(postCommitActions, user);

        logger?.LogWarning(
            "{PaymentObservabilityEvent} Stripe invoice payment failed for correlation {CorrelationId}, customer {StripeCustomerId}, subscription {StripeSubscriptionId}, invoice {StripeInvoiceId}, user {UserId}, attempt {AttemptCount}.",
            "payment_failed",
            eventId,
            customerId,
            subscriptionId,
            GetString(stripeObject, "id"),
            user.Id,
            GetLong(stripeObject, "attempt_count"));
        return null;
    }

    private async Task<string?> SyncInvoicePaymentSucceededAsync(
        AppDbContext db,
        JsonElement stripeObject,
        DateTimeOffset now,
        List<Func<CancellationToken, Task>> postCommitActions,
        CancellationToken cancellationToken)
    {
        var customerId = GetString(stripeObject, "customer");
        var subscriptionId = GetInvoiceSubscriptionId(stripeObject);
        if (string.IsNullOrWhiteSpace(customerId) && string.IsNullOrWhiteSpace(subscriptionId))
        {
            return null;
        }

        var user = await db.AppUsers
            .AsTracking()
            .SingleOrDefaultAsync(
                x => (!string.IsNullOrWhiteSpace(customerId) && x.StripeCustomerId == customerId) ||
                    (!string.IsNullOrWhiteSpace(subscriptionId) && x.StripeSubscriptionId == subscriptionId),
                cancellationToken);
        if (user is null)
        {
            return $"No matching user for Stripe invoice payment_succeeded customer {customerId ?? "unknown"} subscription {subscriptionId ?? "unknown"}.";
        }

        var invoiceSyncFailure = await UpsertStripeInvoiceAsync(
            db,
            user,
            "invoice.payment_succeeded",
            stripeObject,
            now,
            cancellationToken);
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
            EnqueuePaymentRecoveredNotification(postCommitActions, user);
        }

        return null;
    }

    private async Task<string?> SyncStripeInvoiceAsync(
        AppDbContext db,
        string type,
        JsonElement stripeObject,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var customerId = GetString(stripeObject, "customer");
        var subscriptionId = GetInvoiceSubscriptionId(stripeObject);
        if (string.IsNullOrWhiteSpace(customerId) && string.IsNullOrWhiteSpace(subscriptionId))
        {
            return null;
        }

        var user = await FindInvoiceUserAsync(db, customerId, subscriptionId, cancellationToken);
        if (user is null)
        {
            return $"No matching user for Stripe {type} customer {customerId ?? "unknown"} subscription {subscriptionId ?? "unknown"}.";
        }

        return await UpsertStripeInvoiceAsync(db, user, type, stripeObject, now, cancellationToken);
    }

    private async Task<string?> SyncSubscriptionObjectAsync(
        AppDbContext db,
        string type,
        JsonElement stripeObject,
        DateTimeOffset now,
        List<Func<CancellationToken, Task>> postCommitActions,
        CancellationToken cancellationToken)
    {
        var customerId = GetString(stripeObject, "customer");
        if (string.IsNullOrWhiteSpace(customerId))
        {
            return null;
        }

        var user = await db.AppUsers
            .AsTracking()
            .SingleOrDefaultAsync(x => x.StripeCustomerId == customerId, cancellationToken);
        if (user is null)
        {
            return $"No matching user for Stripe subscription customer {customerId}.";
        }

        var rawStatus = type == "customer.subscription.deleted"
            ? "canceled"
            : GetString(stripeObject, "status");
        var status = type == "customer.subscription.deleted"
            ? SubscriptionStatus.Canceled
            : MapSubscriptionStatus(rawStatus);
        var wasInPaymentGrace = HasPaymentGrace(user) || user.SubscriptionStatus == SubscriptionStatus.PastDue;

        user.StripeSubscriptionId = GetString(stripeObject, "id") ?? user.StripeSubscriptionId;
        user.SubscriptionStatus = status;
        user.CurrentPeriodEnd = GetSubscriptionPeriodEnd(stripeObject);
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
                EnqueueSubscriptionPausedNotification(postCommitActions, user);
            }

            ClearPaymentGrace(user);
        }
        else if (status is SubscriptionStatus.Active or SubscriptionStatus.Trialing or SubscriptionStatus.Testing)
        {
            ClearPaymentGrace(user);
        }

        return null;
    }

    private async Task<string?> SyncCheckoutSessionAsync(
        AppDbContext db,
        string eventId,
        JsonElement stripeObject,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var externalAuthUserId = GetString(stripeObject, "client_reference_id") ??
            GetMetadataString(stripeObject, "externalAuthUserId") ??
            GetMetadataString(stripeObject, "clerkUserId");
        var customerId = GetString(stripeObject, "customer");

        if (string.IsNullOrWhiteSpace(externalAuthUserId) && string.IsNullOrWhiteSpace(customerId))
        {
            return null;
        }

        var user = await db.AppUsers.AsTracking().SingleOrDefaultAsync(
            x => (!string.IsNullOrWhiteSpace(externalAuthUserId) && x.ExternalAuthUserId == externalAuthUserId) ||
                 (!string.IsNullOrWhiteSpace(customerId) && x.StripeCustomerId == customerId),
            cancellationToken);
        if (user is null)
        {
            return RequiresCheckoutUser(stripeObject)
                ? $"No matching user for Stripe checkout session customer {customerId ?? "unknown"}."
                : null;
        }

        user.StripeCustomerId = customerId ?? user.StripeCustomerId;
        user.StripeSubscriptionId = GetString(stripeObject, "subscription") ?? user.StripeSubscriptionId;
        user.UpdatedAt = now;
        user.RowVersion = Guid.NewGuid();

        if (IsPaidPaymentSession(stripeObject) &&
            !await db.RewriteCredits.AnyAsync(x => x.StripeEventId == eventId, cancellationToken) &&
            ResolveGrantedRewrites(stripeObject) is { } rewrites)
        {
            var sku = GetMetadataString(stripeObject, "sku");
            db.RewriteCredits.Add(new RewriteCredit
            {
                UserId = user.Id,
                Source = "PURCHASE",
                AmountGranted = rewrites,
                OriginalAmountGranted = rewrites,
                AmountConsumed = 0,
                GrantedAt = now,
                ExpiresAt = now.AddDays(90),
                StripeEventId = eventId,
                StripePaymentIntentId = ResolvePaymentIntentId(stripeObject),
                StripeReceiptUrl = ResolveReceiptUrl(stripeObject),
                StripeSku = sku,
                StripeAmountTotal = GetLong(stripeObject, "amount_total"),
                StripeCurrency = GetString(stripeObject, "currency"),
            });
        }

        return null;
    }

    private static async Task<AppUser?> FindInvoiceUserAsync(
        AppDbContext db,
        string? customerId,
        string? subscriptionId,
        CancellationToken cancellationToken) =>
        await db.AppUsers
            .AsTracking()
            .SingleOrDefaultAsync(
                x => (!string.IsNullOrWhiteSpace(customerId) && x.StripeCustomerId == customerId) ||
                    (!string.IsNullOrWhiteSpace(subscriptionId) && x.StripeSubscriptionId == subscriptionId),
                cancellationToken);

    private static async Task<string?> UpsertStripeInvoiceAsync(
        AppDbContext db,
        AppUser user,
        string type,
        JsonElement stripeObject,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var invoiceId = GetString(stripeObject, "id");
        if (string.IsNullOrWhiteSpace(invoiceId))
        {
            return "Stripe invoice event missing invoice id.";
        }

        var invoice = await db.StripeInvoices
            .AsTracking()
            .SingleOrDefaultAsync(x => x.Id == invoiceId, cancellationToken);
        if (invoice is null)
        {
            invoice = new StripeInvoice
            {
                Id = invoiceId,
                UserId = user.Id,
                Status = ResolveStripeInvoiceStatus(stripeObject, type),
                AmountDue = GetLong(stripeObject, "amount_due") ?? 0,
                AmountPaid = GetLong(stripeObject, "amount_paid") ?? 0,
                Currency = GetString(stripeObject, "currency") ?? string.Empty,
                SubscriptionId = GetInvoiceSubscriptionId(stripeObject),
                PeriodStart = GetInvoicePeriodDate(stripeObject, "period_start", "start"),
                PeriodEnd = GetInvoicePeriodDate(stripeObject, "period_end", "end"),
                AttemptCount = GetInt32(stripeObject, "attempt_count"),
                NextPaymentAttempt = GetUnixDateTime(stripeObject, "next_payment_attempt"),
                HostedInvoiceUrl = GetString(stripeObject, "hosted_invoice_url"),
                InvoicePdf = GetString(stripeObject, "invoice_pdf"),
                CreatedAt = now,
                UpdatedAt = now,
            };
            db.StripeInvoices.Add(invoice);
            return null;
        }

        invoice.UserId = user.Id;
        invoice.SubscriptionId = GetInvoiceSubscriptionId(stripeObject);
        invoice.Status = ResolveStripeInvoiceStatus(stripeObject, type);
        invoice.AmountDue = GetLong(stripeObject, "amount_due") ?? 0;
        invoice.AmountPaid = GetLong(stripeObject, "amount_paid") ?? 0;
        invoice.Currency = GetString(stripeObject, "currency") ?? string.Empty;
        invoice.PeriodStart = GetInvoicePeriodDate(stripeObject, "period_start", "start");
        invoice.PeriodEnd = GetInvoicePeriodDate(stripeObject, "period_end", "end");
        invoice.AttemptCount = GetInt32(stripeObject, "attempt_count");
        invoice.NextPaymentAttempt = GetUnixDateTime(stripeObject, "next_payment_attempt");
        invoice.HostedInvoiceUrl = GetString(stripeObject, "hosted_invoice_url");
        invoice.InvoicePdf = GetString(stripeObject, "invoice_pdf");
        invoice.UpdatedAt = now;
        invoice.RowVersion = Guid.NewGuid();
        return null;
    }

    private async Task RunPostCommitActionsAsync(
        IReadOnlyList<Func<CancellationToken, Task>> postCommitActions,
        CancellationToken cancellationToken)
    {
        foreach (var action in postCommitActions)
        {
            try
            {
                await action(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger?.LogError(ex, "Stripe webhook post-commit action failed.");
            }
        }
    }

    private void EnqueueFailedPaymentNotification(
        List<Func<CancellationToken, Task>> postCommitActions,
        AppUser user)
    {
        if (notificationService is null || string.IsNullOrWhiteSpace(user.Email))
        {
            return;
        }

        var recipient = CreateRecipient(user);
        var externalAuthUserId = user.ExternalAuthUserId;
        postCommitActions.Add(async cancellationToken =>
        {
            var billingPortalUrl = await ResolveBillingPortalUrlAsync(externalAuthUserId, cancellationToken);
            await notificationService.SendAsync(
                NotificationTemplates.FailedPayment,
                recipient,
                new FailedPaymentNotificationModel("there", SupportEmail, billingPortalUrl),
                cancellationToken);
        });
    }

    private void EnqueueSubscriptionPausedNotification(
        List<Func<CancellationToken, Task>> postCommitActions,
        AppUser user)
    {
        if (notificationService is null || string.IsNullOrWhiteSpace(user.Email))
        {
            return;
        }

        var recipient = CreateRecipient(user);
        postCommitActions.Add(cancellationToken => notificationService.SendAsync(
            NotificationTemplates.SubscriptionPaused,
            recipient,
            new SubscriptionPausedNotificationModel("there", SupportEmail),
            cancellationToken));
    }

    private void EnqueueCancelStripeSubscription(
        List<Func<CancellationToken, Task>> postCommitActions,
        AppUser user)
    {
        if (stripeBillingService is null || string.IsNullOrWhiteSpace(user.StripeSubscriptionId))
        {
            return;
        }

        var stripeSubscriptionId = user.StripeSubscriptionId;
        postCommitActions.Add(cancellationToken =>
            stripeBillingService.CancelSubscriptionAsync(stripeSubscriptionId, cancellationToken));
    }

    private void EnqueuePaymentRecoveredNotification(
        List<Func<CancellationToken, Task>> postCommitActions,
        AppUser user)
    {
        if (notificationService is null || string.IsNullOrWhiteSpace(user.Email))
        {
            return;
        }

        var recipient = CreateRecipient(user);
        postCommitActions.Add(cancellationToken => notificationService.SendAsync(
            NotificationTemplates.PaymentRecovered,
            recipient,
            new PaymentRecoveredNotificationModel("there", SupportEmail),
            cancellationToken));
    }

    private async Task<string> ResolveBillingPortalUrlAsync(
        string externalAuthUserId,
        CancellationToken cancellationToken)
    {
        if (stripeBillingService is null)
        {
            return "https://replyinmyvoice.com/app";
        }

        try
        {
            return await stripeBillingService.CreatePortalSessionUrlAsync(externalAuthUserId, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger?.LogWarning(
                ex,
                "Could not create Stripe billing portal session for failed-payment notification.");
            return "https://replyinmyvoice.com/app";
        }
    }

    private static NotificationRecipient CreateRecipient(AppUser user) =>
        new(user.Email!, null);

    private static bool HasPaymentGrace(AppUser user) =>
        user.PaymentFailedAt is not null || user.PaymentGraceEndsAt is not null;

    private static void ClearPaymentGrace(AppUser user)
    {
        user.PaymentFailedAt = null;
        user.PaymentGraceEndsAt = null;
    }

    private static DateTimeOffset ResolvePaymentGraceEndsAt(
        JsonElement stripeObject,
        DateTimeOffset? currentPeriodEnd,
        DateTimeOffset now)
    {
        var graceEndsAt = GetUnixDateTime(stripeObject, "next_payment_attempt") ??
            GetUnixDateTime(stripeObject, "due_date") ??
            GetUnixDateTime(stripeObject, "current_period_end") ??
            currentPeriodEnd;

        if (graceEndsAt is { } candidate && candidate > now)
        {
            return candidate;
        }

        return now.AddDays(DefaultPaymentGraceDays);
    }

    private static bool IsTerminalDunningStatus(string? status)
    {
        var normalized = status?.Trim().ToLowerInvariant();
        return normalized is "unpaid" or "canceled";
    }

    private async Task<string?> RevokeRefundedChargeCreditsAsync(
        AppDbContext db,
        JsonElement stripeObject,
        CancellationToken cancellationToken)
    {
        var paymentIntentId = GetString(stripeObject, "payment_intent");
        if (string.IsNullOrWhiteSpace(paymentIntentId))
        {
            return null;
        }

        var credits = await db.RewriteCredits
            .AsTracking()
            .Where(x => x.StripePaymentIntentId == paymentIntentId)
            .ToListAsync(cancellationToken);
        if (credits.Count == 0)
        {
            return $"No matching rewrite credit for Stripe refund payment_intent {paymentIntentId}.";
        }

        foreach (var credit in credits)
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

    private async Task RevokeDisputedChargeCreditsAsync(
        AppDbContext db,
        JsonElement stripeObject,
        CancellationToken cancellationToken)
    {
        var paymentIntentId = GetString(stripeObject, "payment_intent");
        if (string.IsNullOrWhiteSpace(paymentIntentId))
        {
            return;
        }

        var credits = await db.RewriteCredits
            .AsTracking()
            .Where(x => x.StripePaymentIntentId == paymentIntentId)
            .ToListAsync(cancellationToken);

        foreach (var credit in credits)
        {
            if (credit.AmountGranted == credit.AmountConsumed)
            {
                continue;
            }

            credit.AmountGranted = credit.AmountConsumed;
            credit.RowVersion = Guid.NewGuid();
        }
    }

    private static bool IsPaidPaymentSession(JsonElement stripeObject) =>
        GetString(stripeObject, "mode") == "payment" &&
        GetString(stripeObject, "payment_status") == "paid";

    private static string? ResolvePaymentIntentId(JsonElement stripeObject)
    {
        if (!stripeObject.TryGetProperty("payment_intent", out var paymentIntent))
        {
            return null;
        }

        return paymentIntent.ValueKind == JsonValueKind.Object
            ? GetString(paymentIntent, "id")
            : GetString(stripeObject, "payment_intent");
    }

    private static string? ResolveReceiptUrl(JsonElement stripeObject)
    {
        if (!stripeObject.TryGetProperty("payment_intent", out var paymentIntent) ||
            paymentIntent.ValueKind != JsonValueKind.Object ||
            !paymentIntent.TryGetProperty("latest_charge", out var latestCharge) ||
            latestCharge.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var receiptUrl = GetString(latestCharge, "receipt_url");
        return string.IsNullOrWhiteSpace(receiptUrl) ? null : receiptUrl;
    }

    private static bool RequiresCheckoutUser(JsonElement stripeObject) =>
        IsPaidPaymentSession(stripeObject) ||
        GetString(stripeObject, "mode") == "subscription" ||
        !string.IsNullOrWhiteSpace(GetString(stripeObject, "subscription"));

    private static bool IsFullRefund(JsonElement stripeObject)
    {
        if (GetBool(stripeObject, "refunded") == true)
        {
            return true;
        }

        var amount = GetLong(stripeObject, "amount");
        var amountRefunded = GetLong(stripeObject, "amount_refunded");
        return amount is > 0 && amountRefunded >= amount;
    }

    private static int? ResolveRemainingGrantedAfterRefund(JsonElement stripeObject, RewriteCredit credit)
    {
        var amount = GetLong(stripeObject, "amount") ?? credit.StripeAmountTotal;
        var amountRefunded = GetLong(stripeObject, "amount_refunded");
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

        return StripeBillingService.TryGetSkuDefinition(credit.StripeSku, out var definition)
            ? definition!.Rewrites
            : null;
    }

    private static int? ResolveGrantedRewrites(JsonElement stripeObject)
    {
        var metadataRewrites = GetMetadataString(stripeObject, "rewrites");
        if (int.TryParse(metadataRewrites, out var parsedRewrites) && parsedRewrites > 0)
        {
            return parsedRewrites;
        }

        var sku = GetMetadataString(stripeObject, "sku");
        return StripeBillingService.TryGetSkuDefinition(sku, out var definition)
            ? definition!.Rewrites
            : null;
    }

    private static string TruncateStripeEventError(string error) =>
        error.Length > 1000 ? error[..1000] : error;

    private static bool IsStripeEventCreditUniqueConstraintViolation(DbUpdateException exception)
    {
        var message = exception.ToString();
        return message.Contains("IX_RewriteCredits_StripeEventId", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("RewriteCredits.StripeEventId", StringComparison.OrdinalIgnoreCase);
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

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : value.ToString();
    }

    private static string? GetMetadataString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty("metadata", out var metadata) ||
            metadata.ValueKind != JsonValueKind.Object ||
            !metadata.TryGetProperty(propertyName, out var value) ||
            value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return value.GetString();
    }

    private static string? GetInvoiceSubscriptionId(JsonElement stripeObject)
    {
        var subscriptionId = GetString(stripeObject, "subscription");
        if (!string.IsNullOrWhiteSpace(subscriptionId))
        {
            return subscriptionId;
        }

        if (stripeObject.TryGetProperty("parent", out var parent) &&
            parent.ValueKind == JsonValueKind.Object &&
            parent.TryGetProperty("subscription_details", out var details) &&
            details.ValueKind == JsonValueKind.Object)
        {
            return GetString(details, "subscription");
        }

        return null;
    }

    private static long? GetLong(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt64(out var parsed) => parsed,
            JsonValueKind.String when long.TryParse(value.GetString(), out var parsed) => parsed,
            _ => null,
        };
    }

    private static int GetInt32(JsonElement element, string propertyName)
    {
        var value = GetLong(element, propertyName);
        if (value is null)
        {
            return 0;
        }

        return (int)Math.Clamp(value.Value, int.MinValue, int.MaxValue);
    }

    private static bool? GetBool(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(value.GetString(), out var parsed) => parsed,
            _ => null,
        };
    }

    private static DateTimeOffset? GetUnixDateTime(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        var seconds = value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt64(out var parsed) => parsed,
            JsonValueKind.String when long.TryParse(value.GetString(), out var parsed) => parsed,
            _ => (long?)null,
        };

        return seconds is null ? null : DateTimeOffset.FromUnixTimeSeconds(seconds.Value);
    }

    private static string ResolveStripeInvoiceStatus(JsonElement stripeObject, string type)
    {
        var status = GetString(stripeObject, "status");
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

    private static DateTimeOffset? GetInvoicePeriodDate(
        JsonElement stripeObject,
        string topLevelPropertyName,
        string nestedPropertyName)
    {
        var topLevelDate = GetUnixDateTime(stripeObject, topLevelPropertyName);
        if (topLevelDate is not null)
        {
            return topLevelDate;
        }

        if (!stripeObject.TryGetProperty("lines", out var lines) ||
            lines.ValueKind != JsonValueKind.Object ||
            !lines.TryGetProperty("data", out var data) ||
            data.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var line in data.EnumerateArray())
        {
            if (line.ValueKind != JsonValueKind.Object ||
                !line.TryGetProperty("period", out var period) ||
                period.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var date = GetUnixDateTime(period, nestedPropertyName);
            if (date is not null)
            {
                return date;
            }
        }

        return null;
    }

    private static DateTimeOffset? GetSubscriptionPeriodEnd(JsonElement stripeObject)
    {
        var topLevelPeriodEnd = GetUnixDateTime(stripeObject, "current_period_end");
        if (topLevelPeriodEnd is not null)
        {
            return topLevelPeriodEnd;
        }

        if (!stripeObject.TryGetProperty("items", out var items))
        {
            return null;
        }

        if (items.ValueKind == JsonValueKind.Array)
        {
            return GetFirstItemPeriodEnd(items);
        }

        if (items.ValueKind == JsonValueKind.Object &&
            items.TryGetProperty("data", out var data) &&
            data.ValueKind == JsonValueKind.Array)
        {
            return GetFirstItemPeriodEnd(data);
        }

        return null;
    }

    private static DateTimeOffset? GetFirstItemPeriodEnd(JsonElement items)
    {
        foreach (var item in items.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var periodEnd = GetUnixDateTime(item, "current_period_end");
            if (periodEnd is not null)
            {
                return periodEnd;
            }
        }

        return null;
    }

    private sealed record StripeFailureLogInfo(int AttemptCount, StripeEventStatus Status);
}
