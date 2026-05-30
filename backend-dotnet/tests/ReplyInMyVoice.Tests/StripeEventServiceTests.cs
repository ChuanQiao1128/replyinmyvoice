using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
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
}
