using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Infrastructure.Data;
using ReplyInMyVoice.Infrastructure.Notifications;
using ReplyInMyVoice.Infrastructure.Services;

namespace ReplyInMyVoice.Tests;

public sealed class StripeEventServiceTests
{
    [Fact]
    public async Task TryMarkProcessedAsync_returns_false_for_duplicate_event_id()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var service = new StripeEventService(fixture.CreateContext);

        var first = await service.TryMarkProcessedAsync("evt_123", "customer.subscription.updated", DateTimeOffset.UtcNow);
        var second = await service.TryMarkProcessedAsync("evt_123", "customer.subscription.updated", DateTimeOffset.UtcNow);

        first.Should().BeTrue();
        second.Should().BeFalse();
    }

    [Fact]
    public async Task ProcessWebhookEventAsync_failed_sync_can_be_retried_and_processed()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var service = new StripeEventService(fixture.CreateContext);

        var act = async () => await service.ProcessWebhookEventAsync(
            "evt_retry",
            "checkout.session.completed",
            "{not-valid-json",
            DateTimeOffset.Parse("2026-05-20T00:00:00Z"));
        await act.Should().ThrowAsync<System.Text.Json.JsonException>();

        var retry = await service.ProcessWebhookEventAsync(
            "evt_retry",
            "checkout.session.completed",
            $$"""
            {
              "id": "evt_retry",
              "type": "checkout.session.completed",
              "data": {
                "object": {
                  "client_reference_id": "{{user.ExternalAuthUserId}}",
                  "customer": "cus_retry",
                  "subscription": "sub_retry"
                }
              }
            }
            """,
            DateTimeOffset.Parse("2026-05-20T00:00:10Z"));

        retry.Should().BeTrue();
        await using var db = fixture.CreateContext();
        var storedEvent = await db.StripeEvents.SingleAsync(x => x.EventId == "evt_retry");
        storedEvent.Status.Should().Be(StripeEventStatus.Processed);
        storedEvent.AttemptCount.Should().Be(2);
        storedEvent.LastError.Should().BeNull();

        var updatedUser = await db.AppUsers.SingleAsync(x => x.Id == user.Id);
        updatedUser.StripeCustomerId.Should().Be("cus_retry");
        updatedUser.StripeSubscriptionId.Should().Be("sub_retry");
    }

    [Fact]
    public async Task ProcessWebhookEventAsync_paid_checkout_session_grants_rewrite_credit_once_per_event()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var service = new StripeEventService(fixture.CreateContext);
        var now = DateTimeOffset.Parse("2026-05-25T00:00:00Z");

        var first = await service.ProcessWebhookEventAsync(
            "evt_paid_pack",
            "checkout.session.completed",
            $$"""
            {
              "id": "evt_paid_pack",
              "type": "checkout.session.completed",
              "data": {
                "object": {
                  "client_reference_id": "{{user.ExternalAuthUserId}}",
                  "customer": "cus_pack",
                  "mode": "payment",
                  "payment_status": "paid",
                  "metadata": {
                    "sku": "value_pack",
                    "rewrites": "30",
                    "externalAuthUserId": "{{user.ExternalAuthUserId}}"
                  }
                }
              }
            }
            """,
            now);
        var second = await service.ProcessWebhookEventAsync(
            "evt_paid_pack",
            "checkout.session.completed",
            "{}",
            now.AddSeconds(1));

        first.Should().BeTrue();
        second.Should().BeFalse();

        await using var db = fixture.CreateContext();
        var credit = await db.RewriteCredits.SingleAsync();
        credit.UserId.Should().Be(user.Id);
        credit.Source.Should().Be("PURCHASE");
        credit.AmountGranted.Should().Be(30);
        credit.AmountConsumed.Should().Be(0);
        credit.StripeEventId.Should().Be("evt_paid_pack");
        credit.GrantedAt.Should().Be(now);
        credit.ExpiresAt.Should().NotBeNull();
        credit.ExpiresAt!.Value.Should().BeCloseTo(now.AddDays(90), TimeSpan.FromSeconds(1));

        var updatedUser = await db.AppUsers.SingleAsync(x => x.Id == user.Id);
        updatedUser.StripeCustomerId.Should().Be("cus_pack");
    }

    [Fact]
    public async Task ProcessWebhookEventAsync_PaidCheckoutWithoutUserCanBeReplayedAfterUserCreated()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var service = new StripeEventService(fixture.CreateContext);
        var externalAuthUserId = $"clerk_{Guid.NewGuid():N}";
        var now = DateTimeOffset.Parse("2026-05-25T00:05:00Z");
        var rawBody = $$"""
        {
          "id": "evt_orphan_paid_pack",
          "type": "checkout.session.completed",
          "data": {
            "object": {
              "client_reference_id": "{{externalAuthUserId}}",
              "customer": "cus_orphan_pack",
              "mode": "payment",
              "payment_status": "paid",
              "payment_intent": "pi_orphan_pack",
              "amount_total": 1200,
              "currency": "nzd",
              "metadata": {
                "sku": "quick_pack",
                "rewrites": "10",
                "externalAuthUserId": "{{externalAuthUserId}}"
              }
            }
          }
        }
        """;

        var orphanResult = await service.ProcessWebhookEventAsync(
            "evt_orphan_paid_pack",
            "checkout.session.completed",
            rawBody,
            now);

        orphanResult.Should().BeFalse();
        await using (var orphanDb = fixture.CreateContext())
        {
            var storedEvent = await orphanDb.StripeEvents.SingleAsync(x => x.EventId == "evt_orphan_paid_pack");
            storedEvent.Status.Should().Be(StripeEventStatus.Failed);
            storedEvent.ProcessedAt.Should().BeNull();
            storedEvent.AttemptCount.Should().Be(1);
            storedEvent.LastError.Should().Contain("No matching user");
            (await orphanDb.RewriteCredits.CountAsync()).Should().Be(0);
        }

        await using (var seedDb = fixture.CreateContext())
        {
            seedDb.AppUsers.Add(new AppUser
            {
                ExternalAuthUserId = externalAuthUserId,
                Email = "orphan-replay@example.com",
                SubscriptionStatus = SubscriptionStatus.Inactive,
                CreatedAt = now.AddSeconds(30),
                UpdatedAt = now.AddSeconds(30),
            });
            await seedDb.SaveChangesAsync();
        }

        var replayResult = await service.ProcessWebhookEventAsync(
            "evt_orphan_paid_pack",
            "checkout.session.completed",
            rawBody,
            now.AddMinutes(1));
        var duplicateResult = await service.ProcessWebhookEventAsync(
            "evt_orphan_paid_pack",
            "checkout.session.completed",
            rawBody,
            now.AddMinutes(2));

        replayResult.Should().BeTrue();
        duplicateResult.Should().BeFalse();

        await using var db = fixture.CreateContext();
        var processedEvent = await db.StripeEvents.SingleAsync(x => x.EventId == "evt_orphan_paid_pack");
        processedEvent.Status.Should().Be(StripeEventStatus.Processed);
        processedEvent.AttemptCount.Should().Be(2);
        processedEvent.ProcessedAt.Should().Be(now.AddMinutes(1));
        processedEvent.LastError.Should().BeNull();

        var credit = await db.RewriteCredits.SingleAsync(x => x.StripeEventId == "evt_orphan_paid_pack");
        credit.AmountGranted.Should().Be(10);
        credit.AmountConsumed.Should().Be(0);
        credit.StripePaymentIntentId.Should().Be("pi_orphan_pack");
        credit.StripeSku.Should().Be("quick_pack");

        var updatedUser = await db.AppUsers.SingleAsync(x => x.ExternalAuthUserId == externalAuthUserId);
        credit.UserId.Should().Be(updatedUser.Id);
        updatedUser.StripeCustomerId.Should().Be("cus_orphan_pack");
    }

    [Fact]
    public async Task ProcessWebhookEventAsync_ConcurrentPaidCheckoutSessionDuplicatesGrantOnceAndDoNotFail()
    {
        const string eventId = "evt_paid_pack_parallel";
        await using var fixture = await CheckoutGrantRaceFixture.CreateAsync(eventId);
        var user = await fixture.CreateUserAsync();
        var service = new StripeEventService(fixture.CreateContext);
        var now = DateTimeOffset.Parse("2026-05-25T00:10:00Z");
        var rawBody = $$"""
        {
          "id": "{{eventId}}",
          "type": "checkout.session.completed",
          "data": {
            "object": {
              "client_reference_id": "{{user.ExternalAuthUserId}}",
              "customer": "cus_pack_parallel",
              "mode": "payment",
              "payment_status": "paid",
              "payment_intent": "pi_pack_parallel",
              "metadata": {
                "sku": "value_pack",
                "rewrites": "30",
                "externalAuthUserId": "{{user.ExternalAuthUserId}}"
              }
            }
          }
        }
        """;

        var tasks = new[]
        {
            service.ProcessWebhookEventAsync(eventId, "checkout.session.completed", rawBody, now),
            service.ProcessWebhookEventAsync(eventId, "checkout.session.completed", rawBody, now.AddMilliseconds(1)),
        };

        bool[] results = [];
        var act = async () => results = await Task.WhenAll(tasks);
        await act.Should().NotThrowAsync();
        await fixture.WaitForConcurrentCreditAsync().WaitAsync(TimeSpan.FromSeconds(5));
        results.Should().HaveCount(2);

        await using var db = fixture.CreateContext();
        var storedEvent = await db.StripeEvents.SingleAsync(x => x.EventId == eventId);
        storedEvent.Status.Should().Be(StripeEventStatus.Processed);
        (await db.StripeEvents.CountAsync(x => x.Status == StripeEventStatus.Failed)).Should().Be(0);

        var credit = await db.RewriteCredits.SingleAsync(x => x.StripeEventId == eventId);
        credit.UserId.Should().Be(user.Id);
        credit.AmountGranted.Should().Be(30);
        credit.AmountConsumed.Should().Be(0);
    }

    [Fact]
    public async Task ProcessWebhookEventAsync_GrantAndProcessedUseSingleTransactionalContext()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var contextOpenCount = 0;

        ReplyInMyVoice.Infrastructure.Data.AppDbContext CreateContext()
        {
            contextOpenCount++;
            if (contextOpenCount == 4)
            {
                throw new InvalidOperationException("Unexpected separate context after entitlement sync.");
            }

            return fixture.CreateContext();
        }

        var service = new StripeEventService(CreateContext);
        var now = DateTimeOffset.Parse("2026-05-26T00:02:00Z");

        var processed = await service.ProcessWebhookEventAsync(
            "evt_atomic_paid_pack",
            "checkout.session.completed",
            $$"""
            {
              "id": "evt_atomic_paid_pack",
              "type": "checkout.session.completed",
              "data": {
                "object": {
                  "client_reference_id": "{{user.ExternalAuthUserId}}",
                  "customer": "cus_atomic_pack",
                  "mode": "payment",
                  "payment_status": "paid",
                  "payment_intent": "pi_atomic_pack",
                  "amount_total": 1200,
                  "currency": "nzd",
                  "metadata": {
                    "sku": "quick_pack",
                    "rewrites": "10",
                    "externalAuthUserId": "{{user.ExternalAuthUserId}}"
                  }
                }
              }
            }
            """,
            now);

        processed.Should().BeTrue();
        contextOpenCount.Should().BeLessThan(4);

        await using var db = fixture.CreateContext();
        var storedEvent = await db.StripeEvents.SingleAsync(x => x.EventId == "evt_atomic_paid_pack");
        storedEvent.Status.Should().Be(StripeEventStatus.Processed);

        var credit = await db.RewriteCredits.SingleAsync(x => x.StripeEventId == "evt_atomic_paid_pack");
        credit.UserId.Should().Be(user.Id);
        credit.AmountGranted.Should().Be(10);
        credit.StripePaymentIntentId.Should().Be("pi_atomic_pack");
    }

    [Fact]
    public async Task ProcessWebhookEventAsync_GrantCapturesPaymentIdentifiers()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var service = new StripeEventService(fixture.CreateContext);
        var now = DateTimeOffset.Parse("2026-05-26T00:00:00Z");

        var processed = await service.ProcessWebhookEventAsync(
            "evt_paid_identifiers",
            "checkout.session.completed",
            $$"""
            {
              "id": "evt_paid_identifiers",
              "type": "checkout.session.completed",
              "data": {
                "object": {
                  "client_reference_id": "{{user.ExternalAuthUserId}}",
                  "customer": "cus_identifiers",
                  "mode": "payment",
                  "payment_status": "paid",
                  "payment_intent": "pi_123",
                  "amount_total": 1200,
                  "currency": "nzd",
                  "metadata": {
                    "sku": "quick_pack",
                    "rewrites": "10",
                    "externalAuthUserId": "{{user.ExternalAuthUserId}}"
                  }
                }
              }
            }
            """,
            now);

        processed.Should().BeTrue();

        await using var db = fixture.CreateContext();
        var credit = await db.RewriteCredits.SingleAsync();
        credit.StripeEventId.Should().Be("evt_paid_identifiers");
        credit.StripePaymentIntentId.Should().Be("pi_123");
        credit.StripeSku.Should().Be("quick_pack");
        credit.StripeAmountTotal.Should().Be(1200);
        credit.StripeCurrency.Should().Be("nzd");
    }

    [Fact]
    public async Task ProcessWebhookEventAsync_DuplicateSubscriptionEventDoesNotSyncAgain()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        await using (var seedDb = fixture.CreateContext())
        {
            var storedUser = await seedDb.AppUsers.SingleAsync(x => x.Id == user.Id);
            storedUser.StripeCustomerId = "cus_subscription_duplicate";
            await seedDb.SaveChangesAsync();
        }

        var service = new StripeEventService(fixture.CreateContext);
        var now = DateTimeOffset.Parse("2026-05-26T00:04:00Z");

        var first = await service.ProcessWebhookEventAsync(
            "evt_subscription_duplicate",
            "customer.subscription.updated",
            """
            {
              "id": "evt_subscription_duplicate",
              "type": "customer.subscription.updated",
              "data": {
                "object": {
                  "id": "sub_active",
                  "customer": "cus_subscription_duplicate",
                  "status": "active",
                  "current_period_end": 1780000000
                }
              }
            }
            """,
            now);
        var second = await service.ProcessWebhookEventAsync(
            "evt_subscription_duplicate",
            "customer.subscription.deleted",
            """
            {
              "id": "evt_subscription_duplicate",
              "type": "customer.subscription.deleted",
              "data": {
                "object": {
                  "id": "sub_canceled",
                  "customer": "cus_subscription_duplicate",
                  "status": "canceled",
                  "current_period_end": 1780000100
                }
              }
            }
            """,
            now.AddSeconds(1));

        first.Should().BeTrue();
        second.Should().BeFalse();

        await using var db = fixture.CreateContext();
        var updatedUser = await db.AppUsers.SingleAsync(x => x.Id == user.Id);
        updatedUser.SubscriptionStatus.Should().Be(SubscriptionStatus.Active);
        updatedUser.StripeSubscriptionId.Should().Be("sub_active");
        updatedUser.CurrentPeriodEnd.Should().Be(DateTimeOffset.FromUnixTimeSeconds(1780000000));
    }

    [Fact]
    public async Task ProcessWebhookEventAsync_SubscriptionItemsLevelCurrentPeriodEndSyncsUserPeriod()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        await using (var seedDb = fixture.CreateContext())
        {
            var storedUser = await seedDb.AppUsers.SingleAsync(x => x.Id == user.Id);
            storedUser.StripeCustomerId = "cus_subscription_items_period";
            await seedDb.SaveChangesAsync();
        }

        var service = new StripeEventService(fixture.CreateContext);
        var now = DateTimeOffset.Parse("2026-05-26T00:06:00Z");

        var processed = await service.ProcessWebhookEventAsync(
            "evt_subscription_items_period",
            "customer.subscription.updated",
            """
            {
              "id": "evt_subscription_items_period",
              "type": "customer.subscription.updated",
              "data": {
                "object": {
                  "id": "sub_items_period",
                  "customer": "cus_subscription_items_period",
                  "status": "active",
                  "items": {
                    "data": [
                      {
                        "id": "si_items_period",
                        "current_period_end": 1780000200
                      }
                    ]
                  }
                }
              }
            }
            """,
            now);

        processed.Should().BeTrue();

        await using var db = fixture.CreateContext();
        var updatedUser = await db.AppUsers.SingleAsync(x => x.Id == user.Id);
        updatedUser.SubscriptionStatus.Should().Be(SubscriptionStatus.Active);
        updatedUser.StripeSubscriptionId.Should().Be("sub_items_period");
        updatedUser.CurrentPeriodEnd.Should().Be(DateTimeOffset.FromUnixTimeSeconds(1780000200));
    }

    [Fact]
    public async Task ProcessWebhookEventAsync_InvoicePaymentFailedEntersGraceAndSendsNotificationOnce()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var now = DateTimeOffset.Parse("2026-05-26T00:09:00Z");
        await using (var seedDb = fixture.CreateContext())
        {
            var storedUser = await seedDb.AppUsers.SingleAsync(x => x.Id == user.Id);
            storedUser.Email = "customer@example.com";
            storedUser.StripeCustomerId = "cus_invoice_failed_dunning";
            storedUser.StripeSubscriptionId = "sub_invoice_failed_dunning";
            storedUser.SubscriptionStatus = SubscriptionStatus.Active;
            await seedDb.SaveChangesAsync();
        }

        var notifications = new RecordingNotificationService();
        var billing = new FakeDunningStripeBillingService("https://billing.test/portal");
        var service = new StripeEventService(
            fixture.CreateContext,
            notificationService: notifications,
            stripeBillingService: billing);
        const string rawBody = """
        {
          "id": "evt_invoice_failed_dunning",
          "type": "invoice.payment_failed",
          "data": {
            "object": {
              "id": "in_failed_dunning",
              "customer": "cus_invoice_failed_dunning",
              "subscription": "sub_invoice_failed_dunning",
              "attempt_count": 1,
              "next_payment_attempt": 1780000300
            }
          }
        }
        """;

        var first = await service.ProcessWebhookEventAsync(
            "evt_invoice_failed_dunning",
            "invoice.payment_failed",
            rawBody,
            now);
        var replay = await service.ProcessWebhookEventAsync(
            "evt_invoice_failed_dunning",
            "invoice.payment_failed",
            rawBody,
            now.AddSeconds(1));

        first.Should().BeTrue();
        replay.Should().BeFalse();

        await using var db = fixture.CreateContext();
        var updatedUser = await db.AppUsers.SingleAsync(x => x.Id == user.Id);
        updatedUser.SubscriptionStatus.Should().Be(SubscriptionStatus.PastDue);
        updatedUser.PaymentFailedAt.Should().Be(now);
        updatedUser.PaymentGraceEndsAt
            .Should()
            .Be(DateTimeOffset.FromUnixTimeSeconds(1780000300));

        AccountService.GetUsagePlan(updatedUser).Scope.Should().Be("paid");
        notifications.Messages.Should().ContainSingle();
        notifications.Messages[0].TemplateName.Should().Be("failed-payment");
        notifications.Messages[0].Recipient.Email.Should().Be("customer@example.com");
        notifications.Messages[0].Model.Should().BeOfType<FailedPaymentNotificationModel>()
            .Which.BillingPortalUrl.Should().Be("https://billing.test/portal");
        billing.PortalRequests.Should().Equal(user.ExternalAuthUserId);
    }

    [Fact]
    public async Task ProcessExpiredPaymentGraceAsync_DowngradesAndSendsPausedNotificationOnce()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var now = DateTimeOffset.Parse("2026-05-26T00:20:00Z");
        await using (var seedDb = fixture.CreateContext())
        {
            var storedUser = await seedDb.AppUsers.SingleAsync(x => x.Id == user.Id);
            storedUser.Email = "customer@example.com";
            storedUser.SubscriptionStatus = SubscriptionStatus.PastDue;
            storedUser.PaymentFailedAt = now.AddDays(-3);
            storedUser.PaymentGraceEndsAt = now.AddSeconds(-1);
            await seedDb.SaveChangesAsync();
        }

        var notifications = new RecordingNotificationService();
        var service = new StripeEventService(fixture.CreateContext, notificationService: notifications);

        var first = await service.ProcessExpiredPaymentGraceAsync(now);
        var replay = await service.ProcessExpiredPaymentGraceAsync(now.AddSeconds(1));

        first.Should().Be(1);
        replay.Should().Be(0);

        await using var db = fixture.CreateContext();
        var updatedUser = await db.AppUsers.SingleAsync(x => x.Id == user.Id);
        updatedUser.SubscriptionStatus.Should().Be(SubscriptionStatus.Inactive);
        updatedUser.PaymentFailedAt.Should().BeNull();
        updatedUser.PaymentGraceEndsAt.Should().BeNull();
        AccountService.GetUsagePlan(updatedUser).Scope.Should().Be("free");

        notifications.Messages.Should().ContainSingle();
        notifications.Messages[0].TemplateName.Should().Be("subscription-paused");
        notifications.Messages[0].Recipient.Email.Should().Be("customer@example.com");
    }

    [Fact]
    public async Task ProcessWebhookEventAsync_InvoicePaymentSucceededAfterFailureClearsGraceAndSendsRecoveredNotificationOnce()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var now = DateTimeOffset.Parse("2026-05-26T00:30:00Z");
        await using (var seedDb = fixture.CreateContext())
        {
            var storedUser = await seedDb.AppUsers.SingleAsync(x => x.Id == user.Id);
            storedUser.Email = "customer@example.com";
            storedUser.StripeCustomerId = "cus_invoice_recovered";
            storedUser.StripeSubscriptionId = "sub_invoice_recovered";
            storedUser.SubscriptionStatus = SubscriptionStatus.PastDue;
            storedUser.PaymentFailedAt = now.AddDays(-1);
            storedUser.PaymentGraceEndsAt = now.AddDays(2);
            await seedDb.SaveChangesAsync();
        }

        var notifications = new RecordingNotificationService();
        var service = new StripeEventService(fixture.CreateContext, notificationService: notifications);
        const string rawBody = """
        {
          "id": "evt_invoice_recovered",
          "type": "invoice.payment_succeeded",
          "data": {
            "object": {
              "id": "in_recovered",
              "customer": "cus_invoice_recovered",
              "subscription": "sub_invoice_recovered"
            }
          }
        }
        """;

        var first = await service.ProcessWebhookEventAsync(
            "evt_invoice_recovered",
            "invoice.payment_succeeded",
            rawBody,
            now);
        var replay = await service.ProcessWebhookEventAsync(
            "evt_invoice_recovered",
            "invoice.payment_succeeded",
            rawBody,
            now.AddSeconds(1));

        first.Should().BeTrue();
        replay.Should().BeFalse();

        await using var db = fixture.CreateContext();
        var updatedUser = await db.AppUsers.SingleAsync(x => x.Id == user.Id);
        updatedUser.SubscriptionStatus.Should().Be(SubscriptionStatus.Active);
        updatedUser.PaymentFailedAt.Should().BeNull();
        updatedUser.PaymentGraceEndsAt.Should().BeNull();

        notifications.Messages.Should().ContainSingle();
        notifications.Messages[0].TemplateName.Should().Be("payment-recovered");
        notifications.Messages[0].Recipient.Email.Should().Be("customer@example.com");
    }

    [Fact]
    public async Task ProcessWebhookEventAsync_TerminalSubscriptionStatusFromGraceDowngradesAndSendsPausedNotificationOnce()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var now = DateTimeOffset.Parse("2026-05-26T00:40:00Z");
        await using (var seedDb = fixture.CreateContext())
        {
            var storedUser = await seedDb.AppUsers.SingleAsync(x => x.Id == user.Id);
            storedUser.Email = "customer@example.com";
            storedUser.StripeCustomerId = "cus_subscription_terminal";
            storedUser.StripeSubscriptionId = "sub_subscription_terminal";
            storedUser.SubscriptionStatus = SubscriptionStatus.PastDue;
            storedUser.PaymentFailedAt = now.AddDays(-1);
            storedUser.PaymentGraceEndsAt = now.AddDays(1);
            await seedDb.SaveChangesAsync();
        }

        var notifications = new RecordingNotificationService();
        var service = new StripeEventService(fixture.CreateContext, notificationService: notifications);
        const string rawBody = """
        {
          "id": "evt_subscription_terminal",
          "type": "customer.subscription.updated",
          "data": {
            "object": {
              "id": "sub_subscription_terminal",
              "customer": "cus_subscription_terminal",
              "status": "unpaid",
              "current_period_end": 1780000300
            }
          }
        }
        """;

        var first = await service.ProcessWebhookEventAsync(
            "evt_subscription_terminal",
            "customer.subscription.updated",
            rawBody,
            now);
        var replay = await service.ProcessWebhookEventAsync(
            "evt_subscription_terminal",
            "customer.subscription.updated",
            rawBody,
            now.AddSeconds(1));

        first.Should().BeTrue();
        replay.Should().BeFalse();

        await using var db = fixture.CreateContext();
        var updatedUser = await db.AppUsers.SingleAsync(x => x.Id == user.Id);
        updatedUser.SubscriptionStatus.Should().Be(SubscriptionStatus.Inactive);
        updatedUser.PaymentFailedAt.Should().BeNull();
        updatedUser.PaymentGraceEndsAt.Should().BeNull();
        notifications.Messages.Should().ContainSingle();
        notifications.Messages[0].TemplateName.Should().Be("subscription-paused");
        notifications.Messages[0].Recipient.Email.Should().Be("customer@example.com");
    }

    [Theory]
    [InlineData("unpaid")]
    [InlineData("incomplete")]
    [InlineData("incomplete_expired")]
    [InlineData("paused")]
    [InlineData("canceled")]
    public async Task ProcessWebhookEventAsync_NonPayingSubscriptionStatusDowngradesToFreePlan(string stripeStatus)
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        await using (var seedDb = fixture.CreateContext())
        {
            var storedUser = await seedDb.AppUsers.SingleAsync(x => x.Id == user.Id);
            storedUser.StripeCustomerId = "cus_subscription_nonpaying";
            storedUser.StripeSubscriptionId = "sub_subscription_nonpaying";
            storedUser.SubscriptionStatus = SubscriptionStatus.Active;
            storedUser.CurrentPeriodEnd = DateTimeOffset.FromUnixTimeSeconds(1780000300);
            await seedDb.SaveChangesAsync();
        }

        var service = new StripeEventService(fixture.CreateContext);
        var now = DateTimeOffset.Parse("2026-05-26T00:08:00Z");

        var processed = await service.ProcessWebhookEventAsync(
            $"evt_subscription_{stripeStatus}",
            "customer.subscription.updated",
            $$"""
            {
              "id": "evt_subscription_{{stripeStatus}}",
              "type": "customer.subscription.updated",
              "data": {
                "object": {
                  "id": "sub_subscription_nonpaying",
                  "customer": "cus_subscription_nonpaying",
                  "status": "{{stripeStatus}}",
                  "current_period_end": 1780000300
                }
              }
            }
            """,
            now);

        processed.Should().BeTrue();

        await using var db = fixture.CreateContext();
        var updatedUser = await db.AppUsers.SingleAsync(x => x.Id == user.Id);
        updatedUser.SubscriptionStatus.Should().NotBe(SubscriptionStatus.Active);
        updatedUser.SubscriptionStatus.Should().NotBe(SubscriptionStatus.Trialing);
        updatedUser.SubscriptionStatus.Should().NotBe(SubscriptionStatus.Testing);

        var plan = AccountService.GetUsagePlan(updatedUser);
        plan.Scope.Should().Be("free");
        plan.PeriodKey.Should().Be("free:lifetime");
        plan.QuotaLimit.Should().Be(3);
    }

    [Fact]
    public async Task ProcessWebhookEventAsync_SubscriptionPastDueKeepsPaidGraceState()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        await using (var seedDb = fixture.CreateContext())
        {
            var storedUser = await seedDb.AppUsers.SingleAsync(x => x.Id == user.Id);
            storedUser.StripeCustomerId = "cus_subscription_past_due";
            storedUser.StripeSubscriptionId = "sub_subscription_past_due";
            storedUser.SubscriptionStatus = SubscriptionStatus.Active;
            storedUser.CurrentPeriodEnd = DateTimeOffset.FromUnixTimeSeconds(1780000300);
            await seedDb.SaveChangesAsync();
        }

        var service = new StripeEventService(fixture.CreateContext);
        var now = DateTimeOffset.Parse("2026-05-26T00:08:00Z");

        var processed = await service.ProcessWebhookEventAsync(
            "evt_subscription_past_due",
            "customer.subscription.updated",
            """
            {
              "id": "evt_subscription_past_due",
              "type": "customer.subscription.updated",
              "data": {
                "object": {
                  "id": "sub_subscription_past_due",
                  "customer": "cus_subscription_past_due",
                  "status": "past_due",
                  "current_period_end": 1780000300
                }
              }
            }
            """,
            now);

        processed.Should().BeTrue();

        await using var db = fixture.CreateContext();
        var updatedUser = await db.AppUsers.SingleAsync(x => x.Id == user.Id);
        updatedUser.SubscriptionStatus.Should().Be(SubscriptionStatus.PastDue);
        AccountService.GetUsagePlan(updatedUser).Scope.Should().Be("paid");
    }

    [Fact]
    public async Task ProcessWebhookEventAsync_InvoicePaymentFailedLogsOnceAndDedupesEvent()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        await using (var seedDb = fixture.CreateContext())
        {
            var storedUser = await seedDb.AppUsers.SingleAsync(x => x.Id == user.Id);
            storedUser.StripeCustomerId = "cus_invoice_failed";
            storedUser.StripeSubscriptionId = "sub_invoice_failed";
            storedUser.SubscriptionStatus = SubscriptionStatus.Active;
            await seedDb.SaveChangesAsync();
        }

        var logger = new ListLogger<StripeEventService>();
        var service = new StripeEventService(fixture.CreateContext, logger);
        var now = DateTimeOffset.Parse("2026-05-26T00:09:00Z");
        const string rawBody = """
        {
          "id": "evt_invoice_failed",
          "type": "invoice.payment_failed",
          "data": {
            "object": {
              "id": "in_failed",
              "customer": "cus_invoice_failed",
              "subscription": "sub_invoice_failed",
              "attempt_count": 1
            }
          }
        }
        """;

        var first = await service.ProcessWebhookEventAsync(
            "evt_invoice_failed",
            "invoice.payment_failed",
            rawBody,
            now);
        var second = await service.ProcessWebhookEventAsync(
            "evt_invoice_failed",
            "invoice.payment_failed",
            rawBody,
            now.AddSeconds(1));

        first.Should().BeTrue();
        second.Should().BeFalse();

        await using var db = fixture.CreateContext();
        (await db.StripeEvents.CountAsync(x => x.EventId == "evt_invoice_failed")).Should().Be(1);
        (await db.RewriteCredits.CountAsync()).Should().Be(0);

        var invoiceFailureLogs = logger.Records
            .Where(record => record.Message.Contains("Stripe invoice payment failed", StringComparison.Ordinal))
            .ToList();
        invoiceFailureLogs.Should().ContainSingle();
        var state = invoiceFailureLogs.Single().State;
        state["CorrelationId"]?.ToString().Should().Be("evt_invoice_failed");
        state["StripeCustomerId"]?.ToString().Should().Be("cus_invoice_failed");
        state["StripeSubscriptionId"]?.ToString().Should().Be("sub_invoice_failed");
        state["UserId"]?.ToString().Should().Be(user.Id.ToString());
    }

    [Fact]
    public async Task DuplicateEventGrantRejected()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var service = new StripeEventService(fixture.CreateContext);
        var now = DateTimeOffset.Parse("2026-05-26T00:05:00Z");

        var first = await service.ProcessWebhookEventAsync(
            "evt_duplicate_grant",
            "checkout.session.completed",
            $$"""
            {
              "id": "evt_duplicate_grant",
              "type": "checkout.session.completed",
              "data": {
                "object": {
                  "client_reference_id": "{{user.ExternalAuthUserId}}",
                  "customer": "cus_duplicate",
                  "mode": "payment",
                  "payment_status": "paid",
                  "payment_intent": "pi_duplicate",
                  "amount_total": 1200,
                  "currency": "nzd",
                  "metadata": {
                    "sku": "quick_pack",
                    "rewrites": "10",
                    "externalAuthUserId": "{{user.ExternalAuthUserId}}"
                  }
                }
              }
            }
            """,
            now);
        var second = await service.ProcessWebhookEventAsync(
            "evt_duplicate_grant",
            "checkout.session.completed",
            "{}",
            now.AddSeconds(1));

        first.Should().BeTrue();
        second.Should().BeFalse();

        await using var db = fixture.CreateContext();
        (await db.RewriteCredits.CountAsync(x => x.StripeEventId == "evt_duplicate_grant")).Should().Be(1);

        db.RewriteCredits.Add(new RewriteCredit
        {
            UserId = user.Id,
            Source = "PURCHASE",
            AmountGranted = 10,
            AmountConsumed = 0,
            GrantedAt = now.AddMinutes(1),
            StripeEventId = "evt_duplicate_grant",
        });

        var duplicateInsert = async () => await db.SaveChangesAsync();
        await duplicateInsert.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task ProcessWebhookEventAsync_RefundRevokesUnconsumedCredits()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var service = new StripeEventService(fixture.CreateContext);
        var now = DateTimeOffset.Parse("2026-05-30T00:00:00Z");

        await using (var seedDb = fixture.CreateContext())
        {
            seedDb.RewriteCredits.Add(new RewriteCredit
            {
                UserId = user.Id,
                Source = "PURCHASE",
                AmountGranted = 10,
                AmountConsumed = 2,
                GrantedAt = now.AddDays(-1),
                StripePaymentIntentId = "pi_refund",
                StripeSku = "quick_pack",
                StripeAmountTotal = 1200,
                StripeCurrency = "nzd",
            });
            await seedDb.SaveChangesAsync();
        }

        const string refundEvent = """
        {
          "id": "evt_refund_partial",
          "type": "charge.refunded",
          "data": {
            "object": {
              "id": "ch_refund",
              "payment_intent": "pi_refund",
              "amount": 1200,
              "amount_refunded": 600,
              "refunded": false
            }
          }
        }
        """;

        var first = await service.ProcessWebhookEventAsync(
            "evt_refund_partial",
            "charge.refunded",
            refundEvent,
            now);
        var replay = await service.ProcessWebhookEventAsync(
            "evt_refund_partial",
            "charge.refunded",
            refundEvent,
            now.AddSeconds(1));

        first.Should().BeTrue();
        replay.Should().BeFalse();

        await using var db = fixture.CreateContext();
        var credit = await db.RewriteCredits.SingleAsync(x => x.StripePaymentIntentId == "pi_refund");
        credit.AmountGranted.Should().Be(5);
        credit.AmountConsumed.Should().Be(2);
    }

    [Fact]
    public async Task ProcessWebhookEventAsync_SequentialPartialRefundsUseCumulativeOriginalGrant()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var service = new StripeEventService(fixture.CreateContext);
        var now = DateTimeOffset.Parse("2026-05-30T00:05:00Z");

        await using (var seedDb = fixture.CreateContext())
        {
            seedDb.RewriteCredits.Add(new RewriteCredit
            {
                UserId = user.Id,
                Source = "PURCHASE",
                AmountGranted = 10,
                AmountConsumed = 0,
                GrantedAt = now.AddDays(-1),
                StripePaymentIntentId = "pi_sequential_refund",
                StripeSku = "quick_pack",
                StripeAmountTotal = 1000,
                StripeCurrency = "nzd",
            });
            await seedDb.SaveChangesAsync();
        }

        var firstRefund = await service.ProcessWebhookEventAsync(
            "evt_refund_partial_first",
            "charge.refunded",
            """
            {
              "id": "evt_refund_partial_first",
              "type": "charge.refunded",
              "data": {
                "object": {
                  "id": "ch_sequential_refund",
                  "payment_intent": "pi_sequential_refund",
                  "amount": 1000,
                  "amount_refunded": 300,
                  "refunded": false
                }
              }
            }
            """,
            now);
        var secondRefund = await service.ProcessWebhookEventAsync(
            "evt_refund_partial_second",
            "charge.refunded",
            """
            {
              "id": "evt_refund_partial_second",
              "type": "charge.refunded",
              "data": {
                "object": {
                  "id": "ch_sequential_refund",
                  "payment_intent": "pi_sequential_refund",
                  "amount": 1000,
                  "amount_refunded": 600,
                  "refunded": false
                }
              }
            }
            """,
            now.AddSeconds(1));

        firstRefund.Should().BeTrue();
        secondRefund.Should().BeTrue();

        await using var db = fixture.CreateContext();
        var credit = await db.RewriteCredits.SingleAsync(x => x.StripePaymentIntentId == "pi_sequential_refund");
        credit.AmountGranted.Should().Be(4);
        credit.AmountConsumed.Should().Be(0);
    }

    [Fact]
    public async Task ProcessWebhookEventAsync_RefundClampsAndDispute()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var service = new StripeEventService(fixture.CreateContext);
        var now = DateTimeOffset.Parse("2026-05-30T00:10:00Z");

        await using (var seedDb = fixture.CreateContext())
        {
            seedDb.RewriteCredits.AddRange(
                new RewriteCredit
                {
                    UserId = user.Id,
                    Source = "PURCHASE",
                    AmountGranted = 10,
                    AmountConsumed = 8,
                    GrantedAt = now.AddDays(-1),
                    StripePaymentIntentId = "pi_clamp",
                    StripeSku = "quick_pack",
                    StripeAmountTotal = 1200,
                    StripeCurrency = "nzd",
                },
                new RewriteCredit
                {
                    UserId = user.Id,
                    Source = "PURCHASE",
                    AmountGranted = 6,
                    AmountConsumed = 1,
                    GrantedAt = now.AddDays(-1),
                    StripePaymentIntentId = "pi_dispute",
                    StripeAmountTotal = 600,
                    StripeCurrency = "nzd",
                });
            await seedDb.SaveChangesAsync();
        }

        var refund = await service.ProcessWebhookEventAsync(
            "evt_refund_clamp",
            "charge.refunded",
            """
            {
              "id": "evt_refund_clamp",
              "type": "charge.refunded",
              "data": {
                "object": {
                  "id": "ch_clamp",
                  "payment_intent": "pi_clamp",
                  "amount": 1200,
                  "amount_refunded": 600,
                  "refunded": false
                }
              }
            }
            """,
            now);
        var dispute = await service.ProcessWebhookEventAsync(
            "evt_dispute_created",
            "charge.dispute.created",
            """
            {
              "id": "evt_dispute_created",
              "type": "charge.dispute.created",
              "data": {
                "object": {
                  "id": "dp_created",
                  "payment_intent": "pi_dispute",
                  "amount": 600,
                  "status": "needs_response"
                }
              }
            }
            """,
            now.AddSeconds(1));

        refund.Should().BeTrue();
        dispute.Should().BeTrue();

        await using var db = fixture.CreateContext();
        var clamped = await db.RewriteCredits.SingleAsync(x => x.StripePaymentIntentId == "pi_clamp");
        clamped.AmountGranted.Should().Be(8);
        clamped.AmountConsumed.Should().Be(8);

        var disputed = await db.RewriteCredits.SingleAsync(x => x.StripePaymentIntentId == "pi_dispute");
        disputed.AmountGranted.Should().Be(1);
        disputed.AmountConsumed.Should().Be(1);
    }

    private sealed class ListLogger<T> : ILogger<T>
    {
        public List<LogRecord> Records { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull =>
            null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var values = state as IEnumerable<KeyValuePair<string, object?>>;
            var stateValues = values?.ToDictionary(x => x.Key, x => x.Value) ??
                new Dictionary<string, object?>();
            Records.Add(new LogRecord(logLevel, formatter(state, exception), stateValues));
        }
    }

    private sealed record LogRecord(
        LogLevel Level,
        string Message,
        IReadOnlyDictionary<string, object?> State);

    private sealed class RecordingNotificationService : INotificationService
    {
        public List<RecordedNotification> Messages { get; } = [];

        public Task<NotificationSendResult> SendAsync<TModel>(
            NotificationTemplate<TModel> template,
            NotificationRecipient recipient,
            TModel model,
            CancellationToken cancellationToken = default)
        {
            Messages.Add(new RecordedNotification(template.Name, recipient, model!));
            return Task.FromResult(NotificationSendResult.Delivered("recording"));
        }
    }

    private sealed record RecordedNotification(
        string TemplateName,
        NotificationRecipient Recipient,
        object Model);

    private sealed class FakeDunningStripeBillingService(string portalUrl) : IStripeBillingService
    {
        public List<string> PortalRequests { get; } = [];

        public Task<string> CreateCheckoutSessionUrlAsync(
            string externalAuthUserId,
            string? email,
            string? sku,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("Checkout is not used by dunning tests.");

        public Task<string> CreatePortalSessionUrlAsync(
            string externalAuthUserId,
            CancellationToken cancellationToken)
        {
            PortalRequests.Add(externalAuthUserId);
            return Task.FromResult(portalUrl);
        }
    }

    private sealed class CheckoutGrantRaceFixture : IAsyncDisposable
    {
        private readonly string _databasePath;
        private readonly ConcurrentCheckoutGrantSaveInterceptor _saveInterceptor;

        private CheckoutGrantRaceFixture(string databasePath, string eventId)
        {
            _databasePath = databasePath;
            _saveInterceptor = new ConcurrentCheckoutGrantSaveInterceptor(eventId, CreateContextWithoutInterceptor);
        }

        public static async Task<CheckoutGrantRaceFixture> CreateAsync(string eventId)
        {
            var databasePath = Path.Combine(Path.GetTempPath(), $"replyinmyvoice-checkout-race-{Guid.NewGuid():N}.db");
            var fixture = new CheckoutGrantRaceFixture(databasePath, eventId);
            await using var db = fixture.CreateContextWithoutInterceptor();
            await db.Database.EnsureCreatedAsync();
            await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
            return fixture;
        }

        public AppDbContext CreateContext()
        {
            var options = CreateOptionsBuilder()
                .AddInterceptors(_saveInterceptor)
                .Options;
            return new AppDbContext(options);
        }

        public AppDbContext CreateContextWithoutInterceptor()
        {
            var options = CreateOptionsBuilder().Options;
            return new AppDbContext(options);
        }

        public Task WaitForConcurrentCreditAsync() =>
            _saveInterceptor.WaitForConcurrentCreditAsync();

        public async Task<AppUser> CreateUserAsync()
        {
            await using var db = CreateContextWithoutInterceptor();
            var user = new AppUser
            {
                ExternalAuthUserId = $"clerk_{Guid.NewGuid():N}",
                Email = "test@example.com",
                SubscriptionStatus = SubscriptionStatus.Inactive,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            db.AppUsers.Add(user);
            await db.SaveChangesAsync();
            return user;
        }

        public ValueTask DisposeAsync()
        {
            SqliteConnection.ClearAllPools();
            TryDelete(_databasePath);
            TryDelete($"{_databasePath}-wal");
            TryDelete($"{_databasePath}-shm");
            return ValueTask.CompletedTask;
        }

        private DbContextOptionsBuilder<AppDbContext> CreateOptionsBuilder() =>
            new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={_databasePath}")
                .EnableSensitiveDataLogging();

        private static void TryDelete(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private sealed class ConcurrentCheckoutGrantSaveInterceptor : SaveChangesInterceptor
    {
        private readonly string _eventId;
        private readonly Func<AppDbContext> _createContext;
        private readonly TaskCompletionSource _concurrentCreditInserted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _duplicateInserted;

        public ConcurrentCheckoutGrantSaveInterceptor(string eventId, Func<AppDbContext> createContext)
        {
            _eventId = eventId;
            _createContext = createContext;
        }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            var credit = eventData.Context?.ChangeTracker
                .Entries<RewriteCredit>()
                .Where(x => x.State == EntityState.Added)
                .Select(x => x.Entity)
                .SingleOrDefault(x => x.StripeEventId == _eventId);

            if (credit is not null && Interlocked.Exchange(ref _duplicateInserted, 1) == 0)
            {
                _ = InsertConcurrentCreditAfterCurrentSaveFailsAsync(CloneCredit(credit));
                throw new DbUpdateException(
                    "SQLite Error 19: 'UNIQUE constraint failed: RewriteCredits.StripeEventId'. IX_RewriteCredits_StripeEventId");
            }

            return await base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        public Task WaitForConcurrentCreditAsync() =>
            _concurrentCreditInserted.Task;

        private async Task InsertConcurrentCreditAfterCurrentSaveFailsAsync(RewriteCredit credit)
        {
            const int maxAttempts = 40;

            try
            {
                for (var attempt = 1; attempt <= maxAttempts; attempt++)
                {
                    try
                    {
                        await Task.Delay(25);
                        await InsertConcurrentCreditAsync(credit);
                        _concurrentCreditInserted.SetResult();
                        return;
                    }
                    catch (DbUpdateException ex) when (IsDatabaseLocked(ex) && attempt < maxAttempts)
                    {
                    }
                    catch (DbUpdateException ex) when (IsStripeEventCreditUniqueConstraintViolation(ex))
                    {
                        _concurrentCreditInserted.SetResult();
                        return;
                    }
                }

                _concurrentCreditInserted.SetException(new TimeoutException("Concurrent checkout credit insert did not complete."));
            }
            catch (Exception ex)
            {
                _concurrentCreditInserted.SetException(ex);
            }
        }

        private async Task InsertConcurrentCreditAsync(
            RewriteCredit credit)
        {
            await using var db = _createContext();
            db.RewriteCredits.Add(credit);
            await db.SaveChangesAsync();
        }

        private static RewriteCredit CloneCredit(RewriteCredit credit) =>
            new()
            {
                UserId = credit.UserId,
                Source = credit.Source,
                AmountGranted = credit.AmountGranted,
                AmountConsumed = credit.AmountConsumed,
                GrantedAt = credit.GrantedAt,
                ExpiresAt = credit.ExpiresAt,
                StripeEventId = credit.StripeEventId,
                StripePaymentIntentId = credit.StripePaymentIntentId,
                StripeSku = credit.StripeSku,
                StripeAmountTotal = credit.StripeAmountTotal,
                StripeCurrency = credit.StripeCurrency,
            };

        private static bool IsDatabaseLocked(DbUpdateException exception) =>
            exception.ToString().Contains("database is locked", StringComparison.OrdinalIgnoreCase);

        private static bool IsStripeEventCreditUniqueConstraintViolation(DbUpdateException exception)
        {
            var message = exception.ToString();
            return message.Contains("IX_RewriteCredits_StripeEventId", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("RewriteCredits.StripeEventId", StringComparison.OrdinalIgnoreCase);
        }
    }
}
