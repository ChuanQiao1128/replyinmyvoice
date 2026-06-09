using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.Common;
using ReplyInMyVoice.Application.UseCases.StripeEvent;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Infrastructure.Data;
using ReplyInMyVoice.Infrastructure.Repositories;

namespace ReplyInMyVoice.Tests.Application;

public sealed class StripeEventUseCaseTests
{
    [Fact]
    public async Task TryMarkProcessedAsync_returns_false_for_duplicate_event_id()
    {
        await using var fixture = await DbFixture.CreateAsync();
        await using var handlerDb = fixture.CreateContext();
        var handler = new TryMarkStripeEventProcessedHandler(
            new StripeEventRepository(handlerDb),
            new UnitOfWork(handlerDb));
        var now = DateTimeOffset.Parse("2026-06-09T00:00:00Z");

        var first = await handler.HandleAsync(new TryMarkStripeEventProcessedCommand(
            "evt_application_try",
            "customer.subscription.updated",
            now));
        var second = await handler.HandleAsync(new TryMarkStripeEventProcessedCommand(
            "evt_application_try",
            "customer.subscription.updated",
            now.AddSeconds(1)));

        first.Should().BeTrue();
        second.Should().BeFalse();

        await using var verifyDb = fixture.CreateContext();
        var storedEvent = await verifyDb.StripeEvents.SingleAsync();
        storedEvent.EventId.Should().Be("evt_application_try");
        storedEvent.Status.Should().Be(StripeEventStatus.Processed);
        storedEvent.AttemptCount.Should().Be(1);
    }

    [Fact]
    public async Task ProcessWebhookEventAsync_paid_checkout_session_grants_credit_once()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        await using var handlerDb = fixture.CreateContext();
        var handler = CreateWebhookHandler(handlerDb, new RecordingStripeEventNotifier());
        var now = DateTimeOffset.Parse("2026-06-09T00:05:00Z");
        var payload = new StripeWebhookPayloadDto(
            "evt_application_checkout",
            "checkout.session.completed",
            new StripeWebhookObjectDto(
                CustomerId: "cus_application_checkout",
                ExternalAuthUserId: user.ExternalAuthUserId,
                CheckoutMode: "payment",
                PaymentStatus: "paid",
                GrantedRewrites: 30,
                PaymentIntentId: "pi_application_checkout",
                AmountTotal: 1200,
                Currency: "nzd",
                Sku: "value_pack"));

        var first = await handler.HandleAsync(new ProcessStripeWebhookCommand(payload, now));
        var replay = await handler.HandleAsync(new ProcessStripeWebhookCommand(payload, now.AddSeconds(1)));

        first.Should().BeTrue();
        replay.Should().BeFalse();

        await using var verifyDb = fixture.CreateContext();
        var credit = await verifyDb.RewriteCredits.SingleAsync();
        credit.UserId.Should().Be(user.Id);
        credit.Source.Should().Be("PURCHASE");
        credit.AmountGranted.Should().Be(30);
        credit.OriginalAmountGranted.Should().Be(30);
        credit.AmountConsumed.Should().Be(0);
        credit.StripeEventId.Should().Be("evt_application_checkout");
        credit.StripePaymentIntentId.Should().Be("pi_application_checkout");
        credit.StripeAmountTotal.Should().Be(1200);
        credit.StripeCurrency.Should().Be("nzd");

        var storedEvent = await verifyDb.StripeEvents.SingleAsync(x => x.EventId == "evt_application_checkout");
        storedEvent.Status.Should().Be(StripeEventStatus.Processed);
    }

    [Fact]
    public async Task ProcessWebhookEventAsync_invoice_payment_failed_enters_grace_and_notifies_after_commit()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var now = DateTimeOffset.Parse("2026-06-09T00:10:00Z");
        await using (var seedDb = fixture.CreateContext())
        {
            var storedUser = await seedDb.AppUsers.SingleAsync(x => x.Id == user.Id);
            storedUser.Email = "customer@example.com";
            storedUser.StripeCustomerId = "cus_application_invoice";
            storedUser.StripeSubscriptionId = "sub_application_invoice";
            storedUser.SubscriptionStatus = SubscriptionStatus.Active;
            await seedDb.SaveChangesAsync();
        }

        var notifier = new RecordingStripeEventNotifier();
        await using var handlerDb = fixture.CreateContext();
        var handler = CreateWebhookHandler(handlerDb, notifier);
        var payload = new StripeWebhookPayloadDto(
            "evt_application_invoice_failed",
            "invoice.payment_failed",
            new StripeWebhookObjectDto(
                Id: "in_application_failed",
                CustomerId: "cus_application_invoice",
                SubscriptionId: "sub_application_invoice",
                AttemptCount: 2,
                NextPaymentAttempt: now.AddDays(3),
                AmountDue: 900,
                AmountPaid: 0,
                Currency: "nzd"));

        var processed = await handler.HandleAsync(new ProcessStripeWebhookCommand(payload, now));

        processed.Should().BeTrue();
        notifier.Messages.Should().Equal(("failed-payment", user.Id));

        await using var verifyDb = fixture.CreateContext();
        var updatedUser = await verifyDb.AppUsers.SingleAsync(x => x.Id == user.Id);
        updatedUser.SubscriptionStatus.Should().Be(SubscriptionStatus.PastDue);
        updatedUser.PaymentFailedAt.Should().Be(now);
        updatedUser.PaymentGraceEndsAt.Should().Be(now.AddDays(3));
        updatedUser.PaymentGraceReminderSentAt.Should().BeNull();

        var invoice = await verifyDb.StripeInvoices.SingleAsync();
        invoice.Id.Should().Be("in_application_failed");
        invoice.UserId.Should().Be(user.Id);
        invoice.Status.Should().Be("open");
        invoice.AttemptCount.Should().Be(2);
    }

    [Fact]
    public async Task ProcessWebhookEventAsync_subscription_update_syncs_entitlement_state()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        await using (var seedDb = fixture.CreateContext())
        {
            var storedUser = await seedDb.AppUsers.SingleAsync(x => x.Id == user.Id);
            storedUser.StripeCustomerId = "cus_application_subscription";
            storedUser.SubscriptionStatus = SubscriptionStatus.Inactive;
            await seedDb.SaveChangesAsync();
        }

        await using var handlerDb = fixture.CreateContext();
        var handler = CreateWebhookHandler(handlerDb, new RecordingStripeEventNotifier());
        var now = DateTimeOffset.Parse("2026-06-09T00:15:00Z");
        var currentPeriodEnd = DateTimeOffset.Parse("2026-07-09T00:15:00Z");
        var payload = new StripeWebhookPayloadDto(
            "evt_application_subscription",
            "customer.subscription.updated",
            new StripeWebhookObjectDto(
                Id: "sub_application_subscription",
                CustomerId: "cus_application_subscription",
                Status: "active",
                CurrentPeriodEnd: currentPeriodEnd));

        var processed = await handler.HandleAsync(new ProcessStripeWebhookCommand(payload, now));

        processed.Should().BeTrue();

        await using var verifyDb = fixture.CreateContext();
        var updatedUser = await verifyDb.AppUsers.SingleAsync(x => x.Id == user.Id);
        updatedUser.SubscriptionStatus.Should().Be(SubscriptionStatus.Active);
        updatedUser.StripeSubscriptionId.Should().Be("sub_application_subscription");
        updatedUser.CurrentPeriodEnd.Should().Be(currentPeriodEnd);
    }

    [Fact]
    public async Task ProcessExpiredPaymentGraceAsync_drains_expired_users_in_batches()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var firstExpiredUser = await fixture.CreateUserAsync();
        var secondExpiredUser = await fixture.CreateUserAsync();
        var stillInGraceUser = await fixture.CreateUserAsync();
        var now = DateTimeOffset.Parse("2026-06-09T00:20:00Z");
        await using (var seedDb = fixture.CreateContext())
        {
            ConfigurePastDue(seedDb, firstExpiredUser.Id, "sub_first_expired", now.AddDays(-3), now.AddMinutes(-5));
            ConfigurePastDue(seedDb, secondExpiredUser.Id, "sub_second_expired", now.AddDays(-4), now.AddMinutes(-1));
            ConfigurePastDue(seedDb, stillInGraceUser.Id, "sub_still_in_grace", now.AddDays(-1), now.AddDays(2));
            await seedDb.SaveChangesAsync();
        }

        var notifier = new RecordingStripeEventNotifier();
        var cancellation = new RecordingStripeSubscriptionCancellationService();
        await using var handlerDb = fixture.CreateContext();
        var handler = new ProcessExpiredPaymentGraceHandler(
            new AppUserRepository(handlerDb),
            notifier,
            cancellation,
            new UnitOfWork(handlerDb));

        var processed = await handler.HandleAsync(new ProcessExpiredPaymentGraceCommand(now, BatchSize: 1));

        processed.Should().Be(2);
        notifier.Messages.Should().BeEquivalentTo(
        [
            ("subscription-paused", firstExpiredUser.Id),
            ("subscription-paused", secondExpiredUser.Id),
        ]);
        cancellation.SubscriptionIds.Should().BeEquivalentTo(
        [
            "sub_first_expired",
            "sub_second_expired",
        ]);

        await using var verifyDb = fixture.CreateContext();
        var firstUpdated = await verifyDb.AppUsers.SingleAsync(x => x.Id == firstExpiredUser.Id);
        firstUpdated.SubscriptionStatus.Should().Be(SubscriptionStatus.Inactive);
        firstUpdated.PaymentFailedAt.Should().BeNull();
        firstUpdated.PaymentGraceEndsAt.Should().BeNull();

        var secondUpdated = await verifyDb.AppUsers.SingleAsync(x => x.Id == secondExpiredUser.Id);
        secondUpdated.SubscriptionStatus.Should().Be(SubscriptionStatus.Inactive);
        secondUpdated.PaymentFailedAt.Should().BeNull();
        secondUpdated.PaymentGraceEndsAt.Should().BeNull();

        var stillInGrace = await verifyDb.AppUsers.SingleAsync(x => x.Id == stillInGraceUser.Id);
        stillInGrace.SubscriptionStatus.Should().Be(SubscriptionStatus.PastDue);
        stillInGrace.PaymentGraceEndsAt.Should().Be(now.AddDays(2));
    }

    [Fact]
    public async Task ProcessPaymentGraceRemindersAsync_does_not_let_early_candidate_hide_due_user()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var earlyUser = await fixture.CreateUserAsync();
        var dueUser = await fixture.CreateUserAsync();
        var now = DateTimeOffset.Parse("2026-06-09T00:25:00Z");
        await using (var seedDb = fixture.CreateContext())
        {
            ConfigurePastDue(seedDb, earlyUser.Id, "sub_early_reminder", now.AddDays(-1), now.AddDays(1));
            ConfigurePastDue(seedDb, dueUser.Id, "sub_due_reminder", now.AddDays(-5), now.AddDays(3));
            await seedDb.SaveChangesAsync();
        }

        var notifier = new RecordingStripeEventNotifier();
        await using var handlerDb = fixture.CreateContext();
        var handler = new ProcessPaymentGraceRemindersHandler(
            new AppUserRepository(handlerDb),
            notifier,
            new UnitOfWork(handlerDb));

        var processed = await handler.HandleAsync(new ProcessPaymentGraceRemindersCommand(now, BatchSize: 1));

        processed.Should().Be(1);
        notifier.Messages.Should().Equal(("payment-grace-reminder", dueUser.Id));

        await using var verifyDb = fixture.CreateContext();
        var early = await verifyDb.AppUsers.SingleAsync(x => x.Id == earlyUser.Id);
        early.PaymentGraceReminderSentAt.Should().BeNull();

        var due = await verifyDb.AppUsers.SingleAsync(x => x.Id == dueUser.Id);
        due.PaymentGraceReminderSentAt.Should().Be(now);
    }

    private static ProcessStripeWebhookHandler CreateWebhookHandler(
        AppDbContext db,
        IStripeEventNotifier notifier) =>
        new(
            new StripeEventRepository(db),
            new AppUserRepository(db),
            new RewriteCreditRepository(db),
            new StripeInvoiceRepository(db),
            notifier,
            new UnitOfWork(db));

    private static void ConfigurePastDue(
        AppDbContext db,
        Guid userId,
        string subscriptionId,
        DateTimeOffset failedAt,
        DateTimeOffset graceEndsAt)
    {
        var user = db.AppUsers.Single(x => x.Id == userId);
        user.Email = $"{subscriptionId}@example.com";
        user.StripeSubscriptionId = subscriptionId;
        user.SubscriptionStatus = SubscriptionStatus.PastDue;
        user.PaymentFailedAt = failedAt;
        user.PaymentGraceEndsAt = graceEndsAt;
    }

    private sealed class RecordingStripeEventNotifier : IStripeEventNotifier
    {
        public List<(string Kind, Guid UserId)> Messages { get; } = [];

        public Task EnqueueFailedPaymentNotificationAsync(AppUser user, CancellationToken ct = default)
        {
            Messages.Add(("failed-payment", user.Id));
            return Task.CompletedTask;
        }

        public Task EnqueueSubscriptionPausedNotificationAsync(AppUser user, CancellationToken ct = default)
        {
            Messages.Add(("subscription-paused", user.Id));
            return Task.CompletedTask;
        }

        public Task EnqueuePaymentGraceReminderNotificationAsync(AppUser user, CancellationToken ct = default)
        {
            Messages.Add(("payment-grace-reminder", user.Id));
            return Task.CompletedTask;
        }

        public Task EnqueuePaymentRecoveredNotificationAsync(AppUser user, CancellationToken ct = default)
        {
            Messages.Add(("payment-recovered", user.Id));
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingStripeSubscriptionCancellationService : IStripeSubscriptionCancellationService
    {
        public List<string> SubscriptionIds { get; } = [];

        public Task CancelSubscriptionAsync(string stripeSubscriptionId, CancellationToken ct = default)
        {
            SubscriptionIds.Add(stripeSubscriptionId);
            return Task.CompletedTask;
        }
    }
}
