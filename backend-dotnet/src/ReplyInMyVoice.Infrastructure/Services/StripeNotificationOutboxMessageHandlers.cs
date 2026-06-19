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

        await NotifyAsync(user, payload, message.Id, ct);
    }

    protected abstract Task NotifyAsync(
        AppUser user,
        StripeNotificationOutboxPayload payload,
        Guid outboxMessageId,
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
        Guid outboxMessageId,
        CancellationToken ct) =>
        Notifier.EnqueueFailedPaymentNotificationAsync(user, ct, outboxMessageId);
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
        Guid outboxMessageId,
        CancellationToken ct) =>
        Notifier.EnqueuePaymentRecoveredNotificationAsync(user, ct, outboxMessageId);
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
        Guid outboxMessageId,
        CancellationToken ct) =>
        Notifier.EnqueueSubscriptionPausedNotificationAsync(user, ct, outboxMessageId);
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
        Guid outboxMessageId,
        CancellationToken ct) =>
        Notifier.EnqueuePaymentGraceReminderNotificationAsync(user, ct, outboxMessageId);
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
        Guid outboxMessageId,
        CancellationToken ct) =>
        Notifier.EnqueuePaymentActionRequiredNotificationAsync(user, payload.HostedInvoiceUrl, ct, outboxMessageId);
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
        Guid outboxMessageId,
        CancellationToken ct) =>
        Notifier.EnqueueCardExpiringNotificationAsync(
            user,
            payload.Brand,
            payload.Last4,
            payload.ExpMonth,
            payload.ExpYear,
            ct,
            outboxMessageId);
}
