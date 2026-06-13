using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ReplyInMyVoice.Application.Common;
using ReplyInMyVoice.Application.UseCases.StripeEvent;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Infrastructure.Data;
using ReplyInMyVoice.Infrastructure.Repositories;
using ReplyInMyVoice.Infrastructure.Services;

namespace ReplyInMyVoice.Tests.Application;

public sealed class StripeEventProcessingUseCaseTests
{
    [Fact]
    public async Task Ingest_creates_pending_event_with_payload_and_zero_attempts()
    {
        await using var fixture = await DbFixture.CreateAsync();
        await using var db = fixture.CreateContext();
        var handler = CreateIngestHandler(db);
        var now = DateTimeOffset.Parse("2026-06-13T00:00:00Z");
        var rawBody = SubscriptionPayload("evt_ingest_new", "cus_ingest_new", "active");

        var result = await handler.HandleAsync(new IngestStripeWebhookCommand(
            "evt_ingest_new",
            "customer.subscription.updated",
            rawBody,
            now));

        result.Should().Be(StripeWebhookIngestResult.Accepted);
        await using var verifyDb = fixture.CreateContext();
        var stored = await verifyDb.StripeEvents.SingleAsync();
        stored.EventId.Should().Be("evt_ingest_new");
        stored.Status.Should().Be(StripeEventStatus.Pending);
        stored.AttemptCount.Should().Be(0);
        stored.PayloadJson.Should().Be(rawBody);
        stored.LockedUntil.Should().BeNull();
        stored.CreatedAt.Should().Be(now);
    }

    [Fact]
    public async Task Ingest_is_idempotent_and_reports_already_processed()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var now = DateTimeOffset.Parse("2026-06-13T00:01:00Z");
        var rawBody = SubscriptionPayload("evt_ingest_processed", "cus_ingest_processed", "active");
        await using (var ingestDb = fixture.CreateContext())
        {
            var ingest = CreateIngestHandler(ingestDb);
            await ingest.HandleAsync(new IngestStripeWebhookCommand(
                "evt_ingest_processed",
                "customer.subscription.updated",
                rawBody,
                now));
        }

        var processedAt = now.AddSeconds(5);
        await using (var seedDb = fixture.CreateContext())
        {
            var stored = await seedDb.StripeEvents.SingleAsync();
            stored.Status = StripeEventStatus.Processed;
            stored.ProcessedAt = processedAt;
            stored.PayloadJson = null;
            await seedDb.SaveChangesAsync();
        }

        await using (var replayDb = fixture.CreateContext())
        {
            var replay = CreateIngestHandler(replayDb);
            var result = await replay.HandleAsync(new IngestStripeWebhookCommand(
                "evt_ingest_processed",
                "customer.subscription.deleted",
                rawBody.Replace("active", "canceled", StringComparison.Ordinal),
                now.AddMinutes(1)));

            result.Should().Be(StripeWebhookIngestResult.AlreadyProcessed);
        }

        await using var verifyDb = fixture.CreateContext();
        var events = await verifyDb.StripeEvents.ToListAsync();
        events.Should().ContainSingle();
        events[0].Type.Should().Be("customer.subscription.updated");
        events[0].Status.Should().Be(StripeEventStatus.Processed);
        events[0].ProcessedAt.Should().Be(processedAt);
        events[0].PayloadJson.Should().BeNull();
    }

    [Fact]
    public async Task Ingest_rearms_failed_event_to_pending_and_clears_backoff()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var now = DateTimeOffset.Parse("2026-06-13T00:02:00Z");
        await using (var seedDb = fixture.CreateContext())
        {
            seedDb.StripeEvents.Add(new StripeEvent
            {
                EventId = "evt_ingest_failed",
                Type = "checkout.session.completed",
                Status = StripeEventStatus.Failed,
                AttemptCount = 8,
                LastError = "previous error",
                LockedUntil = now.AddHours(1),
                CreatedAt = now.AddMinutes(-10),
                PayloadJson = "{}",
            });
            await seedDb.SaveChangesAsync();
        }

        var rawBody = PaidCheckoutPayload("evt_ingest_failed", "clerk_ingest_rearm");
        await using (var ingestDb = fixture.CreateContext())
        {
            var ingest = CreateIngestHandler(ingestDb);
            var result = await ingest.HandleAsync(new IngestStripeWebhookCommand(
                "evt_ingest_failed",
                "checkout.session.completed",
                rawBody,
                now));

            result.Should().Be(StripeWebhookIngestResult.AlreadyPending);
        }

        await using var verifyDb = fixture.CreateContext();
        var stored = await verifyDb.StripeEvents.SingleAsync();
        stored.Status.Should().Be(StripeEventStatus.Pending);
        stored.AttemptCount.Should().Be(8);
        stored.LockedUntil.Should().BeNull();
        stored.PayloadJson.Should().Be(rawBody);
    }

    [Fact]
    public async Task Processor_processes_due_pending_checkout_event_grants_credit_and_scrubs_payload()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var now = DateTimeOffset.Parse("2026-06-13T00:03:00Z");
        var rawBody = PaidCheckoutPayload("evt_processor_checkout", user.ExternalAuthUserId);
        await IngestAsync(fixture, "evt_processor_checkout", "checkout.session.completed", rawBody, now);

        await using (var processorDb = fixture.CreateContext())
        {
            var processor = CreateProcessor(processorDb);
            var processed = await processor.HandleAsync(new ProcessPendingStripeEventsCommand(now, BatchSize: 10));

            processed.Should().Be(1);
        }

        await using var verifyDb = fixture.CreateContext();
        var credit = await verifyDb.RewriteCredits.SingleAsync();
        credit.UserId.Should().Be(user.Id);
        credit.StripeEventId.Should().Be("evt_processor_checkout");
        credit.AmountGranted.Should().Be(10);

        var stored = await verifyDb.StripeEvents.SingleAsync();
        stored.Status.Should().Be(StripeEventStatus.Processed);
        stored.ProcessedAt.Should().Be(now);
        stored.PayloadJson.Should().BeNull();
        stored.LastError.Should().BeNull();
    }

    [Fact]
    public async Task Processor_schedules_backoff_retry_on_sync_failure_then_poisons_after_max_attempts()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var now = DateTimeOffset.Parse("2026-06-13T00:04:00Z");
        var rawBody = PaidCheckoutPayload("evt_processor_retry", "clerk_missing_user");
        await IngestAsync(fixture, "evt_processor_retry", "checkout.session.completed", rawBody, now);

        DateTimeOffset firstLockedUntil;
        await using (var firstDb = fixture.CreateContext())
        {
            var processor = CreateProcessor(firstDb, maxAttempts: 3);
            (await processor.HandleAsync(new ProcessPendingStripeEventsCommand(now, BatchSize: 1)))
                .Should()
                .Be(0);
        }

        await using (var verifyFirstDb = fixture.CreateContext())
        {
            var stored = await verifyFirstDb.StripeEvents.SingleAsync();
            stored.Status.Should().Be(StripeEventStatus.Pending);
            stored.AttemptCount.Should().Be(1);
            stored.LastError.Should().Contain("No matching user");
            stored.LockedUntil.Should().NotBeNull();
            firstLockedUntil = stored.LockedUntil!.Value;
            (firstLockedUntil - now).Should().Be(TimeSpan.FromMinutes(2));
        }

        var secondNow = firstLockedUntil.AddSeconds(1);
        DateTimeOffset secondLockedUntil;
        await using (var secondDb = fixture.CreateContext())
        {
            var processor = CreateProcessor(secondDb, maxAttempts: 3);
            (await processor.HandleAsync(new ProcessPendingStripeEventsCommand(secondNow, BatchSize: 1)))
                .Should()
                .Be(0);
        }

        await using (var verifySecondDb = fixture.CreateContext())
        {
            var stored = await verifySecondDb.StripeEvents.SingleAsync();
            stored.Status.Should().Be(StripeEventStatus.Pending);
            stored.AttemptCount.Should().Be(2);
            secondLockedUntil = stored.LockedUntil!.Value;
            (secondLockedUntil - secondNow).Should().Be(TimeSpan.FromMinutes(4));
            (secondLockedUntil - secondNow).Should().BeGreaterThan(firstLockedUntil - now);
        }

        var thirdNow = secondLockedUntil.AddSeconds(1);
        await using (var thirdDb = fixture.CreateContext())
        {
            var processor = CreateProcessor(thirdDb, maxAttempts: 3);
            (await processor.HandleAsync(new ProcessPendingStripeEventsCommand(thirdNow, BatchSize: 1)))
                .Should()
                .Be(0);
        }

        await using (var verifyThirdDb = fixture.CreateContext())
        {
            var stored = await verifyThirdDb.StripeEvents.SingleAsync();
            stored.Status.Should().Be(StripeEventStatus.Failed);
            stored.AttemptCount.Should().Be(3);
            stored.LockedUntil.Should().BeNull();
            stored.LastError.Should().Contain("No matching user");
            stored.PayloadJson.Should().Be(rawBody);
        }

        await using (var fourthDb = fixture.CreateContext())
        {
            var processor = CreateProcessor(fourthDb, maxAttempts: 3);
            (await processor.HandleAsync(new ProcessPendingStripeEventsCommand(thirdNow.AddHours(2), BatchSize: 1)))
                .Should()
                .Be(0);
        }

        await using var finalDb = fixture.CreateContext();
        (await finalDb.StripeEvents.SingleAsync()).AttemptCount.Should().Be(3);
    }

    [Fact]
    public async Task Processor_skips_event_with_active_processing_lease_and_reclaims_stale_lease()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var now = DateTimeOffset.Parse("2026-06-13T00:05:00Z");
        var rawBody = PaidCheckoutPayload("evt_processor_lease", user.ExternalAuthUserId);
        await using (var seedDb = fixture.CreateContext())
        {
            seedDb.StripeEvents.Add(new StripeEvent
            {
                EventId = "evt_processor_lease",
                Type = "checkout.session.completed",
                Status = StripeEventStatus.Processing,
                AttemptCount = 0,
                CreatedAt = now.AddMinutes(-1),
                LockedUntil = now.AddMinutes(1),
                PayloadJson = rawBody,
            });
            await seedDb.SaveChangesAsync();
        }

        await using (var activeLeaseDb = fixture.CreateContext())
        {
            var processor = CreateProcessor(activeLeaseDb);
            (await processor.HandleAsync(new ProcessPendingStripeEventsCommand(now, BatchSize: 1)))
                .Should()
                .Be(0);
        }

        await using (var staleSeedDb = fixture.CreateContext())
        {
            var stored = await staleSeedDb.StripeEvents.SingleAsync();
            stored.LockedUntil = now.AddSeconds(-1);
            await staleSeedDb.SaveChangesAsync();
        }

        await using (var reclaimDb = fixture.CreateContext())
        {
            var processor = CreateProcessor(reclaimDb);
            (await processor.HandleAsync(new ProcessPendingStripeEventsCommand(now, BatchSize: 1)))
                .Should()
                .Be(1);
        }

        await using var verifyDb = fixture.CreateContext();
        var processed = await verifyDb.StripeEvents.SingleAsync();
        processed.Status.Should().Be(StripeEventStatus.Processed);
        processed.AttemptCount.Should().Be(1);
        (await verifyDb.RewriteCredits.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Processor_processes_batch_in_created_at_order()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var now = DateTimeOffset.Parse("2026-06-13T00:06:00Z");
        await using (var seedDb = fixture.CreateContext())
        {
            var storedUser = await seedDb.AppUsers.SingleAsync(x => x.Id == user.Id);
            storedUser.StripeCustomerId = "cus_processor_order";
            storedUser.SubscriptionStatus = SubscriptionStatus.Inactive;
            seedDb.StripeEvents.AddRange(
                new StripeEvent
                {
                    EventId = "evt_processor_order_newer",
                    Type = "customer.subscription.deleted",
                    Status = StripeEventStatus.Pending,
                    AttemptCount = 0,
                    CreatedAt = now.AddSeconds(1),
                    PayloadJson = SubscriptionPayload(
                        "evt_processor_order_newer",
                        "cus_processor_order",
                        "canceled",
                        type: "customer.subscription.deleted"),
                },
                new StripeEvent
                {
                    EventId = "evt_processor_order_older",
                    Type = "customer.subscription.updated",
                    Status = StripeEventStatus.Pending,
                    AttemptCount = 0,
                    CreatedAt = now,
                    PayloadJson = SubscriptionPayload(
                        "evt_processor_order_older",
                        "cus_processor_order",
                        "active"),
                });
            await seedDb.SaveChangesAsync();
        }

        await using (var processorDb = fixture.CreateContext())
        {
            var processor = CreateProcessor(processorDb);
            (await processor.HandleAsync(new ProcessPendingStripeEventsCommand(now.AddMinutes(1), BatchSize: 10)))
                .Should()
                .Be(2);
        }

        await using var verifyDb = fixture.CreateContext();
        var updatedUser = await verifyDb.AppUsers.SingleAsync(x => x.Id == user.Id);
        updatedUser.SubscriptionStatus.Should().Be(SubscriptionStatus.Canceled);
    }

    [Fact]
    public async Task Processor_poisons_event_with_missing_payload()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var now = DateTimeOffset.Parse("2026-06-13T00:07:00Z");
        await using (var seedDb = fixture.CreateContext())
        {
            seedDb.StripeEvents.Add(new StripeEvent
            {
                EventId = "evt_processor_missing_payload",
                Type = "customer.subscription.updated",
                Status = StripeEventStatus.Processing,
                AttemptCount = 0,
                CreatedAt = now.AddMinutes(-1),
                LockedUntil = now.AddSeconds(-1),
                PayloadJson = null,
            });
            await seedDb.SaveChangesAsync();
        }

        await using (var processorDb = fixture.CreateContext())
        {
            var processor = CreateProcessor(processorDb);
            (await processor.HandleAsync(new ProcessPendingStripeEventsCommand(now, BatchSize: 1)))
                .Should()
                .Be(0);
        }

        await using var verifyDb = fixture.CreateContext();
        var stored = await verifyDb.StripeEvents.SingleAsync();
        stored.Status.Should().Be(StripeEventStatus.Failed);
        stored.LockedUntil.Should().BeNull();
        stored.LastError.Should().Be("payload_missing");
    }

    [Fact]
    public async Task Processor_marks_processed_on_checkout_grant_unique_conflict()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var now = DateTimeOffset.Parse("2026-06-13T00:08:00Z");
        var rawBody = PaidCheckoutPayload("evt_processor_credit_conflict", user.ExternalAuthUserId);
        await using (var seedDb = fixture.CreateContext())
        {
            seedDb.RewriteCredits.Add(new RewriteCredit
            {
                UserId = user.Id,
                Source = "PURCHASE",
                AmountGranted = 10,
                AmountConsumed = 0,
                GrantedAt = now.AddMinutes(-1),
                StripeEventId = "evt_processor_credit_conflict",
            });
            seedDb.StripeEvents.Add(new StripeEvent
            {
                EventId = "evt_processor_credit_conflict",
                Type = "checkout.session.completed",
                Status = StripeEventStatus.Pending,
                AttemptCount = 0,
                CreatedAt = now,
                PayloadJson = rawBody,
            });
            await seedDb.SaveChangesAsync();
        }

        await using (var processorDb = fixture.CreateContext())
        {
            var processor = CreateProcessor(processorDb);
            (await processor.HandleAsync(new ProcessPendingStripeEventsCommand(now, BatchSize: 1)))
                .Should()
                .Be(1);
        }

        await using var verifyDb = fixture.CreateContext();
        (await verifyDb.RewriteCredits.CountAsync(x => x.StripeEventId == "evt_processor_credit_conflict"))
            .Should()
            .Be(1);
        var stored = await verifyDb.StripeEvents.SingleAsync();
        stored.Status.Should().Be(StripeEventStatus.Processed);
        stored.PayloadJson.Should().BeNull();
    }

    private static async Task IngestAsync(
        DbFixture fixture,
        string eventId,
        string type,
        string rawBody,
        DateTimeOffset now)
    {
        await using var db = fixture.CreateContext();
        var ingest = CreateIngestHandler(db);
        await ingest.HandleAsync(new IngestStripeWebhookCommand(eventId, type, rawBody, now));
    }

    private static IngestStripeWebhookHandler CreateIngestHandler(AppDbContext db) =>
        new(new StripeEventRepository(db), new UnitOfWork(db));

    private static ProcessPendingStripeEventsHandler CreateProcessor(
        AppDbContext db,
        int maxAttempts = 8) =>
        new(
            new StripeEventRepository(db),
            new StripeEventPayloadSynchronizer(
                new AppUserRepository(db),
                new RewriteCreditRepository(db),
                new StripeInvoiceRepository(db),
                new OutboxMessageRepository(db)),
            new RewriteCreditRepository(db),
            new UnitOfWork(db),
            new StripeEventProcessingOptions(maxAttempts, InlineBudgetSeconds: 8),
            NullLogger<ProcessPendingStripeEventsHandler>.Instance);

    private static string PaidCheckoutPayload(string eventId, string externalAuthUserId) =>
        $$"""
        {
          "id": "{{eventId}}",
          "type": "checkout.session.completed",
          "data": {
            "object": {
              "id": "cs_{{eventId}}",
              "client_reference_id": "{{externalAuthUserId}}",
              "customer": "cus_{{eventId}}",
              "mode": "payment",
              "payment_status": "paid",
              "payment_intent": "pi_{{eventId}}",
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

    private static string SubscriptionPayload(
        string eventId,
        string customerId,
        string status,
        string type = "customer.subscription.updated") =>
        $$"""
        {
          "id": "{{eventId}}",
          "type": "{{type}}",
          "data": {
            "object": {
              "id": "sub_{{eventId}}",
              "customer": "{{customerId}}",
              "status": "{{status}}",
              "current_period_end": 1780000300
            }
          }
        }
        """;
}
