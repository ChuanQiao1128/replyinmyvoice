using System.Data;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.Common;
using ReplyInMyVoice.Domain.Enums;

namespace ReplyInMyVoice.Application.UseCases.StripeEvent;

public sealed class ProcessPendingStripeEventsHandler(
    IStripeEventRepository stripeEvents,
    StripeEventPayloadSynchronizer synchronizer,
    IRewriteCreditRepository credits,
    IUnitOfWork unitOfWork,
    StripeEventProcessingOptions options,
    ILogger<ProcessPendingStripeEventsHandler> logger)
{
    private const int ClaimRaceMaxAttempts = 5;
    private static readonly TimeSpan ClaimLease = TimeSpan.FromMinutes(2);

    public async Task<int> HandleAsync(
        ProcessPendingStripeEventsCommand command,
        CancellationToken ct = default)
    {
        var claimed = await unitOfWork.ExecuteInTransactionAsync(
            async transactionCt =>
            {
                var events = await stripeEvents.ClaimDueAsync(
                    command.Now,
                    command.BatchSize,
                    ClaimLease,
                    command.EventId,
                    transactionCt);
                await unitOfWork.SaveChangesAsync(transactionCt);
                return events;
            },
            IsolationLevel.Serializable,
            ClaimRaceMaxAttempts,
            ct);

        var processed = 0;
        foreach (var stripeEvent in claimed)
        {
            if (stripeEvent.PayloadJson is null)
            {
                await MarkPoisonedAsync(
                    stripeEvent.EventId,
                    stripeEvent.Type,
                    "payload_missing",
                    command.Now,
                    ct);
                continue;
            }

            StripeWebhookPayloadDto payload;
            try
            {
                payload = new StripeWebhookPayloadDto(
                    stripeEvent.EventId,
                    stripeEvent.Type,
                    stripeEvent.PayloadJson);
            }
            catch (JsonException ex)
            {
                await MarkFailedAttemptAsync(
                    stripeEvent.EventId,
                    stripeEvent.Type,
                    ex.Message,
                    command.Now,
                    ct);
                continue;
            }

            var postCommitActions = new List<Func<CancellationToken, Task>>();
            try
            {
                var syncFailure = await unitOfWork.ExecuteInTransactionAsync(
                    async transactionCt =>
                    {
                        var failure = await synchronizer.SyncAsync(
                            payload,
                            command.Now,
                            postCommitActions,
                            transactionCt);
                        if (failure is not null)
                        {
                            return failure;
                        }

                        var current = await stripeEvents.GetByEventIdAsync(stripeEvent.EventId, transactionCt);
                        if (current is null || current.Status == StripeEventStatus.Processed)
                        {
                            return null;
                        }

                        stripeEvents.MarkProcessed(current, command.Now);
                        await unitOfWork.SaveChangesAsync(transactionCt);
                        return null;
                    },
                    IsolationLevel.Serializable,
                    ct);

                if (syncFailure is not null)
                {
                    await MarkFailedAttemptAsync(
                        stripeEvent.EventId,
                        stripeEvent.Type,
                        syncFailure,
                        command.Now,
                        ct);
                    continue;
                }

                foreach (var postCommitAction in postCommitActions)
                {
                    try
                    {
                        await postCommitAction(ct);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        logger.LogWarning(
                            ex,
                            "{PaymentObservabilityEvent} Stripe event notification failed for event {EventId} of type {EventType}.",
                            "stripe_event_notification_failed",
                            stripeEvent.EventId,
                            stripeEvent.Type);
                    }
                }

                processed += 1;
            }
            catch (Exception ex)
                when (payload.Type == "checkout.session.completed" &&
                    credits.IsStripeEventIdWriteFailure(ex))
            {
                await MarkProcessedAfterCheckoutGrantConflictAsync(
                    stripeEvent.EventId,
                    stripeEvent.Type,
                    command.Now,
                    ct);
                processed += 1;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await MarkFailedAttemptAsync(
                    stripeEvent.EventId,
                    stripeEvent.Type,
                    ex.Message,
                    command.Now,
                    ct);
            }
        }

        return processed;
    }

    private async Task MarkFailedAttemptAsync(
        string eventId,
        string eventType,
        string error,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var failure = await unitOfWork.ExecuteInTransactionAsync(
            async transactionCt =>
            {
                var current = await stripeEvents.GetByEventIdAsync(eventId, transactionCt);
                if (current is null || current.Status == StripeEventStatus.Processed)
                {
                    return null;
                }

                var maxAttempts = Math.Max(1, options.MaxAttempts);
                if (current.AttemptCount >= maxAttempts)
                {
                    stripeEvents.MarkFailed(current, error, now);
                    await unitOfWork.SaveChangesAsync(transactionCt);
                    return new StripeEventFailureInfo(true, current.AttemptCount, current.Type, error);
                }

                var delayMinutes = Math.Min(60, Math.Pow(2, current.AttemptCount));
                stripeEvents.MarkRetryScheduled(
                    current,
                    error,
                    now.AddMinutes(delayMinutes),
                    now);
                await unitOfWork.SaveChangesAsync(transactionCt);
                return new StripeEventFailureInfo(false, current.AttemptCount, current.Type, error);
            },
            IsolationLevel.Serializable,
            ct);

        if (failure?.Poisoned == true)
        {
            LogPoisoned(eventId, failure.EventType ?? eventType, failure.AttemptCount, failure.Error);
        }
    }

    private async Task MarkPoisonedAsync(
        string eventId,
        string eventType,
        string error,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var failure = await unitOfWork.ExecuteInTransactionAsync(
            async transactionCt =>
            {
                var current = await stripeEvents.GetByEventIdAsync(eventId, transactionCt);
                if (current is null || current.Status == StripeEventStatus.Processed)
                {
                    return null;
                }

                stripeEvents.MarkFailed(current, error, now);
                await unitOfWork.SaveChangesAsync(transactionCt);
                return new StripeEventFailureInfo(true, current.AttemptCount, current.Type, error);
            },
            IsolationLevel.Serializable,
            ct);

        if (failure?.Poisoned == true)
        {
            LogPoisoned(eventId, failure.EventType ?? eventType, failure.AttemptCount, failure.Error);
        }
    }

    private async Task MarkProcessedAfterCheckoutGrantConflictAsync(
        string eventId,
        string eventType,
        DateTimeOffset now,
        CancellationToken ct)
    {
        await unitOfWork.ExecuteInTransactionAsync(
            async transactionCt =>
            {
                var stripeEvent = await stripeEvents.GetByEventIdAsync(eventId, transactionCt);
                if (stripeEvent is null)
                {
                    await stripeEvents.AddAsync(new Domain.Entities.StripeEvent
                    {
                        EventId = eventId,
                        Type = eventType,
                        Status = StripeEventStatus.Processed,
                        AttemptCount = 1,
                        CreatedAt = now,
                        LastAttemptAt = now,
                        ProcessedAt = now,
                    }, transactionCt);
                }
                else if (stripeEvent.Status != StripeEventStatus.Processed)
                {
                    stripeEvent.Type = eventType;
                    stripeEvents.MarkProcessed(stripeEvent, now);
                }

                await unitOfWork.SaveChangesAsync(transactionCt);
            },
            IsolationLevel.Serializable,
            ct);
    }

    private void LogPoisoned(
        string eventId,
        string eventType,
        int attemptCount,
        string error)
    {
        logger.LogError(
            "{PaymentObservabilityEvent} Stripe event {EventId} of type {EventType} poisoned after {AttemptCount} attempts: {Error}",
            "stripe_event_poisoned",
            eventId,
            eventType,
            attemptCount,
            error);
    }

    private sealed record StripeEventFailureInfo(
        bool Poisoned,
        int AttemptCount,
        string? EventType,
        string Error);
}
