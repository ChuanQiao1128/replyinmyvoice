using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.Common;
using ReplyInMyVoice.Application.UseCases.StripeEvent;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Infrastructure.Data;
using ReplyInMyVoice.Infrastructure.Repositories;
using ReplyInMyVoice.Infrastructure.Services;

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
    public async Task ProcessWebhookEventAsync_missing_checkout_user_can_be_replayed_after_user_exists()
    {
        await using var fixture = await DbFixture.CreateAsync();
        await using var handlerDb = fixture.CreateContext();
        var handler = CreateWebhookHandler(handlerDb, new RecordingStripeEventNotifier());
        var externalAuthUserId = $"clerk_{Guid.NewGuid():N}";
        var now = DateTimeOffset.Parse("2026-06-09T00:06:00Z");
        var rawBody = $$"""
        {
          "id": "evt_application_orphan_checkout",
          "type": "checkout.session.completed",
          "data": {
            "object": {
              "client_reference_id": "{{externalAuthUserId}}",
              "customer": "cus_application_orphan",
              "mode": "payment",
              "payment_status": "paid",
              "payment_intent": "pi_application_orphan",
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

        var orphanResult = await HandleWebhookAsync(
            handler,
            "evt_application_orphan_checkout",
            "checkout.session.completed",
            rawBody,
            now);

        orphanResult.Should().BeFalse();
        await using (var orphanDb = fixture.CreateContext())
        {
            var storedEvent = await orphanDb.StripeEvents.SingleAsync(x => x.EventId == "evt_application_orphan_checkout");
            storedEvent.Status.Should().Be(StripeEventStatus.Failed);
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

        var replayResult = await HandleWebhookAsync(
            handler,
            "evt_application_orphan_checkout",
            "checkout.session.completed",
            rawBody,
            now.AddMinutes(1));
        var duplicateResult = await HandleWebhookAsync(
            handler,
            "evt_application_orphan_checkout",
            "checkout.session.completed",
            rawBody,
            now.AddMinutes(2));

        replayResult.Should().BeTrue();
        duplicateResult.Should().BeFalse();

        await using var verifyDb = fixture.CreateContext();
        var processedEvent = await verifyDb.StripeEvents.SingleAsync(x => x.EventId == "evt_application_orphan_checkout");
        processedEvent.Status.Should().Be(StripeEventStatus.Processed);
        processedEvent.AttemptCount.Should().Be(2);
        processedEvent.LastError.Should().BeNull();

        var credit = await verifyDb.RewriteCredits.SingleAsync(x => x.StripeEventId == "evt_application_orphan_checkout");
        credit.AmountGranted.Should().Be(10);
        credit.StripePaymentIntentId.Should().Be("pi_application_orphan");
        credit.StripeSku.Should().Be("quick_pack");

        var updatedUser = await verifyDb.AppUsers.SingleAsync(x => x.ExternalAuthUserId == externalAuthUserId);
        credit.UserId.Should().Be(updatedUser.Id);
        updatedUser.StripeCustomerId.Should().Be("cus_application_orphan");
    }

    [Fact]
    public async Task ProcessWebhookEventAsync_paid_checkout_persists_payment_identifiers_and_expanded_receipt_url()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        await using var handlerDb = fixture.CreateContext();
        var handler = CreateWebhookHandler(handlerDb, new RecordingStripeEventNotifier());
        var now = DateTimeOffset.Parse("2026-06-09T00:07:00Z");

        var processed = await HandleWebhookAsync(
            handler,
            "evt_application_receipt",
            "checkout.session.completed",
            $$"""
            {
              "id": "evt_application_receipt",
              "type": "checkout.session.completed",
              "data": {
                "object": {
                  "client_reference_id": "{{user.ExternalAuthUserId}}",
                  "customer": "cus_application_receipt",
                  "mode": "payment",
                  "payment_status": "paid",
                  "payment_intent": {
                    "id": "pi_application_receipt",
                    "latest_charge": {
                      "id": "ch_application_receipt",
                      "receipt_url": "https://pay.stripe.com/receipts/test_receipt"
                    }
                  },
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

        await using var verifyDb = fixture.CreateContext();
        var credit = await verifyDb.RewriteCredits.SingleAsync();
        credit.StripeEventId.Should().Be("evt_application_receipt");
        credit.StripePaymentIntentId.Should().Be("pi_application_receipt");
        credit.StripeReceiptUrl.Should().Be("https://pay.stripe.com/receipts/test_receipt");
        credit.StripeSku.Should().Be("quick_pack");
        credit.StripeAmountTotal.Should().Be(1200);
        credit.StripeCurrency.Should().Be("nzd");
        credit.ExpiresAt.Should().BeCloseTo(now.AddDays(90), TimeSpan.FromSeconds(1));
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
    public async Task ProcessWebhookEventAsync_duplicate_subscription_event_does_not_sync_again()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        await using (var seedDb = fixture.CreateContext())
        {
            var storedUser = await seedDb.AppUsers.SingleAsync(x => x.Id == user.Id);
            storedUser.StripeCustomerId = "cus_application_duplicate_subscription";
            await seedDb.SaveChangesAsync();
        }

        await using var handlerDb = fixture.CreateContext();
        var handler = CreateWebhookHandler(handlerDb, new RecordingStripeEventNotifier());
        var now = DateTimeOffset.Parse("2026-06-09T00:16:00Z");

        var first = await HandleWebhookAsync(
            handler,
            "evt_application_duplicate_subscription",
            "customer.subscription.updated",
            """
            {
              "id": "evt_application_duplicate_subscription",
              "type": "customer.subscription.updated",
              "data": {
                "object": {
                  "id": "sub_application_active",
                  "customer": "cus_application_duplicate_subscription",
                  "status": "active",
                  "current_period_end": 1780000000
                }
              }
            }
            """,
            now);
        var second = await HandleWebhookAsync(
            handler,
            "evt_application_duplicate_subscription",
            "customer.subscription.deleted",
            """
            {
              "id": "evt_application_duplicate_subscription",
              "type": "customer.subscription.deleted",
              "data": {
                "object": {
                  "id": "sub_application_canceled",
                  "customer": "cus_application_duplicate_subscription",
                  "status": "canceled",
                  "current_period_end": 1780000100
                }
              }
            }
            """,
            now.AddSeconds(1));

        first.Should().BeTrue();
        second.Should().BeFalse();

        await using var verifyDb = fixture.CreateContext();
        var updatedUser = await verifyDb.AppUsers.SingleAsync(x => x.Id == user.Id);
        updatedUser.SubscriptionStatus.Should().Be(SubscriptionStatus.Active);
        updatedUser.StripeSubscriptionId.Should().Be("sub_application_active");
        updatedUser.CurrentPeriodEnd.Should().Be(DateTimeOffset.FromUnixTimeSeconds(1780000000));
    }

    [Fact]
    public async Task ProcessWebhookEventAsync_subscription_items_level_period_syncs_user_period()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        await using (var seedDb = fixture.CreateContext())
        {
            var storedUser = await seedDb.AppUsers.SingleAsync(x => x.Id == user.Id);
            storedUser.StripeCustomerId = "cus_application_items_period";
            await seedDb.SaveChangesAsync();
        }

        await using var handlerDb = fixture.CreateContext();
        var handler = CreateWebhookHandler(handlerDb, new RecordingStripeEventNotifier());
        var now = DateTimeOffset.Parse("2026-06-09T00:17:00Z");

        var processed = await HandleWebhookAsync(
            handler,
            "evt_application_items_period",
            "customer.subscription.updated",
            """
            {
              "id": "evt_application_items_period",
              "type": "customer.subscription.updated",
              "data": {
                "object": {
                  "id": "sub_application_items_period",
                  "customer": "cus_application_items_period",
                  "status": "active",
                  "items": {
                    "data": [
                      {
                        "id": "si_application_items_period",
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

        await using var verifyDb = fixture.CreateContext();
        var updatedUser = await verifyDb.AppUsers.SingleAsync(x => x.Id == user.Id);
        updatedUser.SubscriptionStatus.Should().Be(SubscriptionStatus.Active);
        updatedUser.StripeSubscriptionId.Should().Be("sub_application_items_period");
        updatedUser.CurrentPeriodEnd.Should().Be(DateTimeOffset.FromUnixTimeSeconds(1780000200));
    }

    [Fact]
    public async Task ProcessWebhookEventAsync_invoice_payment_succeeded_after_failure_clears_grace_and_notifies_once()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var now = DateTimeOffset.Parse("2026-06-09T00:18:00Z");
        await using (var seedDb = fixture.CreateContext())
        {
            var storedUser = await seedDb.AppUsers.SingleAsync(x => x.Id == user.Id);
            storedUser.Email = "customer@example.com";
            storedUser.StripeCustomerId = "cus_application_recovered";
            storedUser.StripeSubscriptionId = "sub_application_recovered";
            storedUser.SubscriptionStatus = SubscriptionStatus.PastDue;
            storedUser.PaymentFailedAt = now.AddDays(-1);
            storedUser.PaymentGraceEndsAt = now.AddDays(2);
            await seedDb.SaveChangesAsync();
        }

        var notifier = new RecordingStripeEventNotifier();
        await using var handlerDb = fixture.CreateContext();
        var handler = CreateWebhookHandler(handlerDb, notifier);
        const string rawBody = """
        {
          "id": "evt_application_recovered",
          "type": "invoice.payment_succeeded",
          "data": {
            "object": {
              "id": "in_application_recovered",
              "customer": "cus_application_recovered",
              "subscription": "sub_application_recovered"
            }
          }
        }
        """;

        var first = await HandleWebhookAsync(
            handler,
            "evt_application_recovered",
            "invoice.payment_succeeded",
            rawBody,
            now);
        var replay = await HandleWebhookAsync(
            handler,
            "evt_application_recovered",
            "invoice.payment_succeeded",
            rawBody,
            now.AddSeconds(1));

        first.Should().BeTrue();
        replay.Should().BeFalse();
        notifier.Messages.Should().Equal(("payment-recovered", user.Id));

        await using var verifyDb = fixture.CreateContext();
        var updatedUser = await verifyDb.AppUsers.SingleAsync(x => x.Id == user.Id);
        updatedUser.SubscriptionStatus.Should().Be(SubscriptionStatus.Active);
        updatedUser.PaymentFailedAt.Should().BeNull();
        updatedUser.PaymentGraceEndsAt.Should().BeNull();
    }

    [Fact]
    public async Task ProcessWebhookEventAsync_invoice_paid_upserts_invoice_and_payment_failed_updates_same_row()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        await using (var seedDb = fixture.CreateContext())
        {
            var storedUser = await seedDb.AppUsers.SingleAsync(x => x.Id == user.Id);
            storedUser.StripeCustomerId = "cus_application_invoice_history";
            storedUser.StripeSubscriptionId = "sub_application_invoice_history";
            storedUser.SubscriptionStatus = SubscriptionStatus.Active;
            await seedDb.SaveChangesAsync();
        }

        await using var handlerDb = fixture.CreateContext();
        var handler = CreateWebhookHandler(handlerDb, new RecordingStripeEventNotifier());
        var paidNow = DateTimeOffset.Parse("2026-06-09T00:19:00Z");
        const string paidBody = """
        {
          "id": "evt_application_invoice_paid_history",
          "type": "invoice.paid",
          "data": {
            "object": {
              "id": "in_application_history",
              "customer": "cus_application_invoice_history",
              "subscription": "sub_application_invoice_history",
              "status": "paid",
              "amount_due": 900,
              "amount_paid": 900,
              "currency": "nzd",
              "period_start": 1770000000,
              "period_end": 1772592000,
              "attempt_count": 1,
              "hosted_invoice_url": "https://billing.test/in_application_history",
              "invoice_pdf": "https://billing.test/in_application_history.pdf"
            }
          }
        }
        """;

        var first = await HandleWebhookAsync(
            handler,
            "evt_application_invoice_paid_history",
            "invoice.paid",
            paidBody,
            paidNow);
        var duplicate = await HandleWebhookAsync(
            handler,
            "evt_application_invoice_paid_history",
            "invoice.paid",
            paidBody.Replace("\"amount_paid\": 900", "\"amount_paid\": 1", StringComparison.Ordinal),
            paidNow.AddSeconds(1));

        first.Should().BeTrue();
        duplicate.Should().BeFalse();

        await using (var verifyDb = fixture.CreateContext())
        {
            var invoice = await verifyDb.StripeInvoices.SingleAsync(x => x.Id == "in_application_history");
            invoice.UserId.Should().Be(user.Id);
            invoice.SubscriptionId.Should().Be("sub_application_invoice_history");
            invoice.Status.Should().Be("paid");
            invoice.AmountDue.Should().Be(900);
            invoice.AmountPaid.Should().Be(900);
            invoice.Currency.Should().Be("nzd");
            invoice.PeriodStart.Should().Be(DateTimeOffset.FromUnixTimeSeconds(1770000000));
            invoice.PeriodEnd.Should().Be(DateTimeOffset.FromUnixTimeSeconds(1772592000));
            invoice.AttemptCount.Should().Be(1);
            invoice.HostedInvoiceUrl.Should().Be("https://billing.test/in_application_history");
            invoice.InvoicePdf.Should().Be("https://billing.test/in_application_history.pdf");
            invoice.CreatedAt.Should().Be(paidNow);
            invoice.UpdatedAt.Should().Be(paidNow);
            (await verifyDb.StripeInvoices.CountAsync()).Should().Be(1);
        }

        var failedNow = DateTimeOffset.Parse("2026-06-09T00:20:00Z");
        const string failedBody = """
        {
          "id": "evt_application_invoice_failed_history",
          "type": "invoice.payment_failed",
          "data": {
            "object": {
              "id": "in_application_history",
              "customer": "cus_application_invoice_history",
              "subscription": "sub_application_invoice_history",
              "status": "open",
              "amount_due": 900,
              "amount_paid": 0,
              "currency": "nzd",
              "period_start": 1770000000,
              "period_end": 1772592000,
              "attempt_count": 2,
              "next_payment_attempt": 1773200000,
              "hosted_invoice_url": "https://billing.test/in_application_history",
              "invoice_pdf": "https://billing.test/in_application_history.pdf"
            }
          }
        }
        """;

        var failed = await HandleWebhookAsync(
            handler,
            "evt_application_invoice_failed_history",
            "invoice.payment_failed",
            failedBody,
            failedNow);

        failed.Should().BeTrue();

        await using var db = fixture.CreateContext();
        var updatedInvoice = await db.StripeInvoices.SingleAsync(x => x.Id == "in_application_history");
        updatedInvoice.Status.Should().Be("open");
        updatedInvoice.AmountPaid.Should().Be(0);
        updatedInvoice.AttemptCount.Should().Be(2);
        updatedInvoice.NextPaymentAttempt.Should().Be(DateTimeOffset.FromUnixTimeSeconds(1773200000));
        updatedInvoice.CreatedAt.Should().Be(paidNow);
        updatedInvoice.UpdatedAt.Should().Be(failedNow);
        (await db.StripeInvoices.CountAsync()).Should().Be(1);

        var updatedUser = await db.AppUsers.SingleAsync(x => x.Id == user.Id);
        updatedUser.SubscriptionStatus.Should().Be(SubscriptionStatus.PastDue);
    }

    [Theory]
    [InlineData("unpaid")]
    [InlineData("canceled")]
    public async Task ProcessWebhookEventAsync_terminal_subscription_status_from_grace_downgrades_and_notifies_once(
        string stripeStatus)
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var now = DateTimeOffset.Parse("2026-06-09T00:21:00Z");
        var customerId = $"cus_application_terminal_{stripeStatus}";
        var subscriptionId = $"sub_application_terminal_{stripeStatus}";
        await using (var seedDb = fixture.CreateContext())
        {
            var storedUser = await seedDb.AppUsers.SingleAsync(x => x.Id == user.Id);
            storedUser.Email = "customer@example.com";
            storedUser.StripeCustomerId = customerId;
            storedUser.StripeSubscriptionId = subscriptionId;
            storedUser.SubscriptionStatus = SubscriptionStatus.PastDue;
            storedUser.PaymentFailedAt = now.AddDays(-1);
            storedUser.PaymentGraceEndsAt = now.AddDays(1);
            await seedDb.SaveChangesAsync();
        }

        var notifier = new RecordingStripeEventNotifier();
        await using var handlerDb = fixture.CreateContext();
        var handler = CreateWebhookHandler(handlerDb, notifier);
        var rawBody = $$"""
        {
          "id": "evt_application_terminal_{{stripeStatus}}",
          "type": "customer.subscription.updated",
          "data": {
            "object": {
              "id": "{{subscriptionId}}",
              "customer": "{{customerId}}",
              "status": "{{stripeStatus}}",
              "current_period_end": 1780000300
            }
          }
        }
        """;

        var first = await HandleWebhookAsync(
            handler,
            $"evt_application_terminal_{stripeStatus}",
            "customer.subscription.updated",
            rawBody,
            now);
        var replay = await HandleWebhookAsync(
            handler,
            $"evt_application_terminal_{stripeStatus}",
            "customer.subscription.updated",
            rawBody,
            now.AddSeconds(1));

        first.Should().BeTrue();
        replay.Should().BeFalse();
        notifier.Messages.Should().Equal(("subscription-paused", user.Id));

        await using var verifyDb = fixture.CreateContext();
        var updatedUser = await verifyDb.AppUsers.SingleAsync(x => x.Id == user.Id);
        updatedUser.SubscriptionStatus.Should().Be(SubscriptionStatus.Inactive);
        updatedUser.PaymentFailedAt.Should().BeNull();
        updatedUser.PaymentGraceEndsAt.Should().BeNull();
    }

    [Theory]
    [InlineData("unpaid")]
    [InlineData("incomplete")]
    [InlineData("incomplete_expired")]
    [InlineData("paused")]
    [InlineData("canceled")]
    public async Task ProcessWebhookEventAsync_non_paying_subscription_status_downgrades_to_free_plan(
        string stripeStatus)
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        await using (var seedDb = fixture.CreateContext())
        {
            var storedUser = await seedDb.AppUsers.SingleAsync(x => x.Id == user.Id);
            storedUser.StripeCustomerId = "cus_application_nonpaying";
            storedUser.StripeSubscriptionId = "sub_application_nonpaying";
            storedUser.SubscriptionStatus = SubscriptionStatus.Active;
            storedUser.CurrentPeriodEnd = DateTimeOffset.FromUnixTimeSeconds(1780000300);
            await seedDb.SaveChangesAsync();
        }

        await using var handlerDb = fixture.CreateContext();
        var handler = CreateWebhookHandler(handlerDb, new RecordingStripeEventNotifier());
        var now = DateTimeOffset.Parse("2026-06-09T00:22:00Z");

        var processed = await HandleWebhookAsync(
            handler,
            $"evt_application_nonpaying_{stripeStatus}",
            "customer.subscription.updated",
            $$"""
            {
              "id": "evt_application_nonpaying_{{stripeStatus}}",
              "type": "customer.subscription.updated",
              "data": {
                "object": {
                  "id": "sub_application_nonpaying",
                  "customer": "cus_application_nonpaying",
                  "status": "{{stripeStatus}}",
                  "current_period_end": 1780000300
                }
              }
            }
            """,
            now);

        processed.Should().BeTrue();

        await using var verifyDb = fixture.CreateContext();
        var updatedUser = await verifyDb.AppUsers.SingleAsync(x => x.Id == user.Id);
        updatedUser.SubscriptionStatus.Should().NotBe(SubscriptionStatus.Active);
        updatedUser.SubscriptionStatus.Should().NotBe(SubscriptionStatus.Trialing);
        updatedUser.SubscriptionStatus.Should().NotBe(SubscriptionStatus.Testing);

        var plan = AccountUsagePlans.GetUsagePlan(updatedUser);
        plan.Scope.Should().Be("free");
        plan.PeriodKey.Should().Be("free:lifetime");
        plan.QuotaLimit.Should().Be(0);
    }

    [Fact]
    public async Task ProcessWebhookEventAsync_subscription_past_due_keeps_paid_grace_state()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        await using (var seedDb = fixture.CreateContext())
        {
            var storedUser = await seedDb.AppUsers.SingleAsync(x => x.Id == user.Id);
            storedUser.StripeCustomerId = "cus_application_past_due";
            storedUser.StripeSubscriptionId = "sub_application_past_due";
            storedUser.SubscriptionStatus = SubscriptionStatus.Active;
            storedUser.CurrentPeriodEnd = DateTimeOffset.FromUnixTimeSeconds(1780000300);
            await seedDb.SaveChangesAsync();
        }

        await using var handlerDb = fixture.CreateContext();
        var handler = CreateWebhookHandler(handlerDb, new RecordingStripeEventNotifier());
        var now = DateTimeOffset.Parse("2026-06-09T00:23:00Z");

        var processed = await HandleWebhookAsync(
            handler,
            "evt_application_past_due",
            "customer.subscription.updated",
            """
            {
              "id": "evt_application_past_due",
              "type": "customer.subscription.updated",
              "data": {
                "object": {
                  "id": "sub_application_past_due",
                  "customer": "cus_application_past_due",
                  "status": "past_due",
                  "current_period_end": 1780000300
                }
              }
            }
            """,
            now);

        processed.Should().BeTrue();

        await using var verifyDb = fixture.CreateContext();
        var updatedUser = await verifyDb.AppUsers.SingleAsync(x => x.Id == user.Id);
        updatedUser.SubscriptionStatus.Should().Be(SubscriptionStatus.PastDue);
        AccountUsagePlans.GetUsagePlan(updatedUser).Scope.Should().Be("paid");
    }

    [Fact]
    public async Task ProcessWebhookEventAsync_refund_before_grant_can_be_replayed_after_credit_arrives()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        await using var handlerDb = fixture.CreateContext();
        var handler = CreateWebhookHandler(handlerDb, new RecordingStripeEventNotifier());
        var now = DateTimeOffset.Parse("2026-06-09T00:24:00Z");
        const string refundEvent = """
        {
          "id": "evt_application_refund_before_grant",
          "type": "charge.refunded",
          "data": {
            "object": {
              "id": "ch_application_refund_before_grant",
              "payment_intent": "pi_application_refund_before_grant",
              "amount": 1200,
              "amount_refunded": 600,
              "refunded": false
            }
          }
        }
        """;

        var orphanResult = await HandleWebhookAsync(
            handler,
            "evt_application_refund_before_grant",
            "charge.refunded",
            refundEvent,
            now);

        orphanResult.Should().BeFalse();
        await using (var orphanDb = fixture.CreateContext())
        {
            var storedEvent = await orphanDb.StripeEvents.SingleAsync(x => x.EventId == "evt_application_refund_before_grant");
            storedEvent.Status.Should().Be(StripeEventStatus.Failed);
            storedEvent.AttemptCount.Should().Be(1);
            storedEvent.LastError.Should().Contain("No matching rewrite credit");
            (await orphanDb.RewriteCredits.CountAsync()).Should().Be(0);
        }

        await using (var seedDb = fixture.CreateContext())
        {
            seedDb.RewriteCredits.Add(new RewriteCredit
            {
                UserId = user.Id,
                Source = "PURCHASE",
                AmountGranted = 10,
                AmountConsumed = 0,
                GrantedAt = now.AddSeconds(30),
                StripePaymentIntentId = "pi_application_refund_before_grant",
                StripeSku = "quick_pack",
                StripeAmountTotal = 1200,
                StripeCurrency = "nzd",
            });
            await seedDb.SaveChangesAsync();
        }

        var replayResult = await HandleWebhookAsync(
            handler,
            "evt_application_refund_before_grant",
            "charge.refunded",
            refundEvent,
            now.AddMinutes(1));
        var duplicateResult = await HandleWebhookAsync(
            handler,
            "evt_application_refund_before_grant",
            "charge.refunded",
            refundEvent,
            now.AddMinutes(2));

        replayResult.Should().BeTrue();
        duplicateResult.Should().BeFalse();

        await using var verifyDb = fixture.CreateContext();
        var processedEvent = await verifyDb.StripeEvents.SingleAsync(x => x.EventId == "evt_application_refund_before_grant");
        processedEvent.Status.Should().Be(StripeEventStatus.Processed);
        processedEvent.AttemptCount.Should().Be(2);
        processedEvent.LastError.Should().BeNull();

        var credit = await verifyDb.RewriteCredits.SingleAsync(x => x.StripePaymentIntentId == "pi_application_refund_before_grant");
        credit.AmountGranted.Should().Be(5);
        credit.AmountConsumed.Should().Be(0);
    }

    [Fact]
    public async Task ProcessWebhookEventAsync_refunds_use_original_grant_and_disputes_revoke_unconsumed_credits()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var now = DateTimeOffset.Parse("2026-06-09T00:25:00Z");
        await using (var seedDb = fixture.CreateContext())
        {
            seedDb.RewriteCredits.AddRange(
                new RewriteCredit
                {
                    UserId = user.Id,
                    Source = "PURCHASE",
                    AmountGranted = 7,
                    AmountConsumed = 0,
                    GrantedAt = now.AddDays(-1),
                    StripePaymentIntentId = "pi_application_old_size",
                    StripeSku = "quick_pack",
                    StripeAmountTotal = 700,
                    StripeCurrency = "nzd",
                },
                new RewriteCredit
                {
                    UserId = user.Id,
                    Source = "PURCHASE",
                    AmountGranted = 10,
                    AmountConsumed = 8,
                    GrantedAt = now.AddDays(-1),
                    StripePaymentIntentId = "pi_application_clamp",
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
                    StripePaymentIntentId = "pi_application_dispute",
                    StripeAmountTotal = 600,
                    StripeCurrency = "nzd",
                });
            await seedDb.SaveChangesAsync();
        }

        await using var handlerDb = fixture.CreateContext();
        var handler = CreateWebhookHandler(handlerDb, new RecordingStripeEventNotifier());

        var oldSizeRefund = await HandleWebhookAsync(
            handler,
            "evt_application_old_size_refund",
            "charge.refunded",
            """
            {
              "id": "evt_application_old_size_refund",
              "type": "charge.refunded",
              "data": {
                "object": {
                  "id": "ch_application_old_size",
                  "payment_intent": "pi_application_old_size",
                  "amount": 700,
                  "amount_refunded": 350,
                  "refunded": false
                }
              }
            }
            """,
            now);
        var clampRefund = await HandleWebhookAsync(
            handler,
            "evt_application_clamp_refund",
            "charge.refunded",
            """
            {
              "id": "evt_application_clamp_refund",
              "type": "charge.refunded",
              "data": {
                "object": {
                  "id": "ch_application_clamp",
                  "payment_intent": "pi_application_clamp",
                  "amount": 1200,
                  "amount_refunded": 600,
                  "refunded": false
                }
              }
            }
            """,
            now.AddSeconds(1));
        var dispute = await HandleWebhookAsync(
            handler,
            "evt_application_dispute",
            "charge.dispute.created",
            """
            {
              "id": "evt_application_dispute",
              "type": "charge.dispute.created",
              "data": {
                "object": {
                  "id": "dp_application_dispute",
                  "payment_intent": "pi_application_dispute",
                  "amount": 600,
                  "status": "needs_response"
                }
              }
            }
            """,
            now.AddSeconds(2));

        oldSizeRefund.Should().BeTrue();
        clampRefund.Should().BeTrue();
        dispute.Should().BeTrue();

        await using var verifyDb = fixture.CreateContext();
        var oldSize = await verifyDb.RewriteCredits.SingleAsync(x => x.StripePaymentIntentId == "pi_application_old_size");
        oldSize.AmountGranted.Should().Be(3);
        oldSize.OriginalAmountGranted.Should().Be(7);
        oldSize.AmountConsumed.Should().Be(0);

        var clamped = await verifyDb.RewriteCredits.SingleAsync(x => x.StripePaymentIntentId == "pi_application_clamp");
        clamped.AmountGranted.Should().Be(8);
        clamped.AmountConsumed.Should().Be(8);

        var disputed = await verifyDb.RewriteCredits.SingleAsync(x => x.StripePaymentIntentId == "pi_application_dispute");
        disputed.AmountGranted.Should().Be(1);
        disputed.AmountConsumed.Should().Be(1);
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

    private static async Task<bool> HandleWebhookAsync(
        ProcessStripeWebhookHandler handler,
        string eventId,
        string type,
        string rawBody,
        DateTimeOffset now) =>
        await handler.HandleAsync(new ProcessStripeWebhookCommand(
            new StripeWebhookPayloadDto(eventId, type, rawBody),
            now));

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
