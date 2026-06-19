using System.Text.Json;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.UseCases.StripeEvent;
using ReplyInMyVoice.Domain.Entities;

namespace ReplyInMyVoice.Infrastructure.Services;

internal abstract class StripeNotificationOutboxMessageHandlerBase(
    IAppUserRepository appUsers,
    IStripeEventNotifier notifier) : IOutboxMessageHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    protected IStripeEventNotifier Notifier { get; } = notifier;

    public abstract string MessageType { get; }

    public async Task HandleAsync(OutboxMessage message, CancellationToken ct = default)
    {
        var payload = JsonSerializer.Deserialize<StripeNotificationOutboxPayload>(
            message.PayloadJson,
            JsonOptions);
        if (payload is null || payload.UserId == Guid.Empty)
        {
            throw new JsonException("Outbox payload did not contain a valid user id.");
        }

        var user = await appUsers.GetByIdAsync(payload.UserId, ct);
        if (user is null)
        {
            return;
        }

        // The outbox message id is stable across at-least-once redeliveries of this row, so it is the
        // idempotency key the email provider uses to de-duplicate a crash-induced re-send.
        await NotifyAsync(user, payload, message.Id.ToString(), ct);
    }

    protected abstract Task NotifyAsync(
        AppUser user,
        StripeNotificationOutboxPayload payload,
        string idempotencyKey,
        CancellationToken ct);
}

internal sealed class PaymentFailedNotificationOutboxMessageHandler(
    IAppUserRepository appUsers,
    IStripeEventNotifier notifier)
    : StripeNotificationOutboxMessageHandlerBase(appUsers, notifier)
{
    public override string MessageType => StripeNotificationOutboxMessageTypes.PaymentFailed;

    protected override Task NotifyAsync(
        AppUser user,
        StripeNotificationOutboxPayload payload,
        string idempotencyKey,
        CancellationToken ct) =>
        Notifier.EnqueueFailedPaymentNotificationAsync(user, idempotencyKey, ct);
}

internal sealed class PaymentRecoveredNotificationOutboxMessageHandler(
    IAppUserRepository appUsers,
    IStripeEventNotifier notifier)
    : StripeNotificationOutboxMessageHandlerBase(appUsers, notifier)
{
    public override string MessageType => StripeNotificationOutboxMessageTypes.PaymentRecovered;

    protected override Task NotifyAsync(
        AppUser user,
        StripeNotificationOutboxPayload payload,
        string idempotencyKey,
        CancellationToken ct) =>
        Notifier.EnqueuePaymentRecoveredNotificationAsync(user, idempotencyKey, ct);
}

internal sealed class SubscriptionPausedNotificationOutboxMessageHandler(
    IAppUserRepository appUsers,
    IStripeEventNotifier notifier)
    : StripeNotificationOutboxMessageHandlerBase(appUsers, notifier)
{
    public override string MessageType => StripeNotificationOutboxMessageTypes.SubscriptionPaused;

    protected override Task NotifyAsync(
        AppUser user,
        StripeNotificationOutboxPayload payload,
        string idempotencyKey,
        CancellationToken ct) =>
        Notifier.EnqueueSubscriptionPausedNotificationAsync(user, idempotencyKey, ct);
}

internal sealed class PaymentGraceReminderNotificationOutboxMessageHandler(
    IAppUserRepository appUsers,
    IStripeEventNotifier notifier)
    : StripeNotificationOutboxMessageHandlerBase(appUsers, notifier)
{
    public override string MessageType => StripeNotificationOutboxMessageTypes.PaymentGraceReminder;

    protected override Task NotifyAsync(
        AppUser user,
        StripeNotificationOutboxPayload payload,
        string idempotencyKey,
        CancellationToken ct) =>
        Notifier.EnqueuePaymentGraceReminderNotificationAsync(user, idempotencyKey, ct);
}

internal sealed class StripePaymentActionRequiredOutboxMessageHandler(
    IAppUserRepository appUsers,
    IStripeEventNotifier notifier)
    : StripeNotificationOutboxMessageHandlerBase(appUsers, notifier)
{
    public override string MessageType => StripeNotificationOutboxMessageTypes.PaymentActionRequired;

    protected override Task NotifyAsync(
        AppUser user,
        StripeNotificationOutboxPayload payload,
        string idempotencyKey,
        CancellationToken ct) =>
        Notifier.EnqueuePaymentActionRequiredNotificationAsync(user, payload.HostedInvoiceUrl, idempotencyKey, ct);
}

internal sealed class StripeCardExpiringOutboxMessageHandler(
    IAppUserRepository appUsers,
    IStripeEventNotifier notifier)
    : StripeNotificationOutboxMessageHandlerBase(appUsers, notifier)
{
    public override string MessageType => StripeNotificationOutboxMessageTypes.CardExpiring;

    protected override Task NotifyAsync(
        AppUser user,
        StripeNotificationOutboxPayload payload,
        string idempotencyKey,
        CancellationToken ct) =>
        Notifier.EnqueueCardExpiringNotificationAsync(
            user,
            payload.Brand,
            payload.Last4,
            payload.ExpMonth,
            payload.ExpYear,
            idempotencyKey,
            ct);
}
