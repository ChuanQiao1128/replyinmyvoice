using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Application.Common;
using ReplyInMyVoice.Application.UseCases.Admin;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Infrastructure.Data;
using ReplyInMyVoice.Infrastructure.Repositories;

namespace ReplyInMyVoice.Tests.Application;

public sealed class RequeueDeadLetterHandlerTests
{
    [Fact]
    public async Task RequeueDeadLetterHandler_resets_failed_outbox_message_and_audits()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var now = DateTimeOffset.Parse("2026-06-19T03:00:00Z");
        Guid outboxId;
        Guid deadLetterId;
        await using (var seedDb = fixture.CreateContext())
        {
            var seedOutbox = new OutboxMessage
            {
                MessageType = "RewriteJobCreated",
                PayloadJson = """{"attemptId":"attempt-for-requeue"}""",
                Status = OutboxMessageStatus.Failed,
                CreatedAt = now.AddHours(-1),
                NextAttemptAt = now.AddMinutes(-10),
                AttemptCount = 10,
                MaxAttempts = 10,
                LastError = "handler failed",
                CorrelationId = "corr-outbox",
            };
            seedDb.OutboxMessages.Add(seedOutbox);
            var seedDeadLetter = CreateDeadLetter(
                "OutboxMessage",
                seedOutbox.Id.ToString("D"),
                sourceData: $$"""{"sourceType":"OutboxMessage","sourceId":"{{seedOutbox.Id:D}}","attemptCount":10,"lastError":"handler failed","payloadJson":{{JsonSerializer.Serialize(seedOutbox.PayloadJson)}}}""",
                failureReason: "handler failed",
                createdAt: now.AddMinutes(-1));
            seedDb.DeadLetterMessages.Add(seedDeadLetter);
            await seedDb.SaveChangesAsync();
            outboxId = seedOutbox.Id;
            deadLetterId = seedDeadLetter.Id;
        }

        await using var db = fixture.CreateContext();
        var handler = CreateHandler(db);

        var result = await handler.HandleAsync(
            new RequeueDeadLetterCommand(
                "admin-owner-oid",
                "owner@example.com",
                deadLetterId,
                now),
            CancellationToken.None);

        result.Kind.Should().Be(AdminDeadLetterRequeueResultKind.Success);
        result.Response.Should().NotBeNull();
        result.Response!.Id.Should().Be(deadLetterId);
        result.Response.RequeuedAt.Should().Be(now);

        var outbox = await db.OutboxMessages.SingleAsync(x => x.Id == outboxId);
        outbox.Status.Should().Be(OutboxMessageStatus.Pending);
        outbox.AttemptCount.Should().Be(0);
        outbox.NextAttemptAt.Should().Be(now);
        outbox.LockedBy.Should().BeNull();
        outbox.LockedUntil.Should().BeNull();
        outbox.LastError.Should().BeNull();
        outbox.SentAt.Should().BeNull();

        var deadLetter = await db.DeadLetterMessages.SingleAsync(x => x.Id == deadLetterId);
        deadLetter.RequeuedAt.Should().Be(now);

        var audit = await db.AdminAuditLogs.SingleAsync();
        audit.AdminExternalAuthUserId.Should().Be("admin-owner-oid");
        audit.AdminEmail.Should().Be("owner@example.com");
        audit.Action.Should().Be("requeue_dead_letter");
        audit.TargetUserId.Should().BeNull();
        using var details = JsonDocument.Parse(audit.DetailsJson!);
        details.RootElement.GetProperty("sourceType").GetString().Should().Be("OutboxMessage");
        details.RootElement.GetProperty("sourceId").GetString().Should().Be(outboxId.ToString("D"));
        details.RootElement.GetProperty("attemptCount").GetInt32().Should().Be(10);
        details.RootElement.GetProperty("lastError").GetString().Should().Be("handler failed");
    }

    [Fact]
    public async Task RequeueDeadLetterHandler_resets_failed_stripe_event_and_preserves_event_id()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var now = DateTimeOffset.Parse("2026-06-19T04:00:00Z");
        Guid deadLetterId;
        await using (var seedDb = fixture.CreateContext())
        {
            var seedStripeEvent = new StripeEvent
            {
                EventId = "evt_requeue_stripe",
                Type = "checkout.session.completed",
                Status = StripeEventStatus.Failed,
                AttemptCount = 3,
                LastError = "No matching user",
                PayloadJson = """{"id":"evt_requeue_stripe","type":"checkout.session.completed"}""",
                LastAttemptAt = now.AddMinutes(-5),
                LockedUntil = null,
                CreatedAt = now.AddHours(-1),
                ProcessedAt = now.AddMinutes(-20),
            };
            seedDb.StripeEvents.Add(seedStripeEvent);
            var deadLetter = CreateDeadLetter(
                "StripeEvent",
                seedStripeEvent.EventId,
                sourceData: """{"sourceType":"StripeEvent","sourceId":"evt_requeue_stripe","attemptCount":3,"lastError":"No matching user"}""",
                failureReason: "No matching user",
                createdAt: now.AddMinutes(-1));
            seedDb.DeadLetterMessages.Add(deadLetter);
            await seedDb.SaveChangesAsync();
            deadLetterId = deadLetter.Id;
        }

        await using var db = fixture.CreateContext();
        var handler = CreateHandler(db);

        var result = await handler.HandleAsync(
            new RequeueDeadLetterCommand(
                "admin-owner-oid",
                "owner@example.com",
                deadLetterId,
                now),
            CancellationToken.None);

        result.Kind.Should().Be(AdminDeadLetterRequeueResultKind.Success);
        var stripeEvent = await db.StripeEvents.SingleAsync(x => x.EventId == "evt_requeue_stripe");
        stripeEvent.EventId.Should().Be("evt_requeue_stripe");
        stripeEvent.Status.Should().Be(StripeEventStatus.Pending);
        stripeEvent.AttemptCount.Should().Be(0);
        stripeEvent.ProcessedAt.Should().BeNull();
        stripeEvent.LockedUntil.Should().BeNull();
        stripeEvent.LastError.Should().BeNull();
        stripeEvent.PayloadJson.Should().Contain("evt_requeue_stripe");

        var audit = await db.AdminAuditLogs.SingleAsync();
        using var details = JsonDocument.Parse(audit.DetailsJson!);
        details.RootElement.GetProperty("sourceType").GetString().Should().Be("StripeEvent");
        details.RootElement.GetProperty("sourceId").GetString().Should().Be("evt_requeue_stripe");
        details.RootElement.GetProperty("attemptCount").GetInt32().Should().Be(3);
        details.RootElement.GetProperty("lastError").GetString().Should().Be("No matching user");
    }

    [Fact]
    public async Task RequeueDeadLetterHandler_is_idempotent_and_returns_conflict_after_first_requeue()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var now = DateTimeOffset.Parse("2026-06-19T05:00:00Z");
        Guid deadLetterId;
        await using (var seedDb = fixture.CreateContext())
        {
            var seedOutbox = new OutboxMessage
            {
                MessageType = "RewriteJobCreated",
                PayloadJson = """{"attemptId":"repeat-requeue"}""",
                Status = OutboxMessageStatus.Failed,
                CreatedAt = now.AddHours(-1),
                NextAttemptAt = now.AddMinutes(-10),
                AttemptCount = 10,
                MaxAttempts = 10,
                LastError = "handler failed",
            };
            seedDb.OutboxMessages.Add(seedOutbox);
            var deadLetter = CreateDeadLetter(
                "OutboxMessage",
                seedOutbox.Id.ToString("D"),
                sourceData: $$"""{"sourceType":"OutboxMessage","sourceId":"{{seedOutbox.Id:D}}","attemptCount":10,"lastError":"handler failed"}""",
                failureReason: "handler failed",
                createdAt: now.AddMinutes(-1));
            seedDb.DeadLetterMessages.Add(deadLetter);
            await seedDb.SaveChangesAsync();
            deadLetterId = deadLetter.Id;
        }

        await using var db = fixture.CreateContext();
        var handler = CreateHandler(db);

        var first = await handler.HandleAsync(
            new RequeueDeadLetterCommand("admin-owner-oid", "owner@example.com", deadLetterId, now),
            CancellationToken.None);
        var second = await handler.HandleAsync(
            new RequeueDeadLetterCommand("admin-owner-oid", "owner@example.com", deadLetterId, now.AddSeconds(1)),
            CancellationToken.None);

        first.Kind.Should().Be(AdminDeadLetterRequeueResultKind.Success);
        second.Kind.Should().Be(AdminDeadLetterRequeueResultKind.AlreadyRequeued);
        (await db.AdminAuditLogs.CountAsync(x => x.Action == "requeue_dead_letter")).Should().Be(1);
        (await db.DeadLetterMessages.SingleAsync(x => x.Id == deadLetterId)).RequeuedAt.Should().Be(now);
    }

    private static RequeueDeadLetterHandler CreateHandler(AppDbContext db) =>
        new(
            new DeadLetterMessageRepository(db),
            new OutboxMessageRepository(db),
            new StripeEventRepository(db),
            new AdminUserRepository(db),
            new UnitOfWork(db));

    private static DeadLetterMessage CreateDeadLetter(
        string sourceType,
        string sourceId,
        string sourceData,
        string failureReason,
        DateTimeOffset createdAt) =>
        new()
        {
            SourceType = sourceType,
            SourceId = sourceId,
            SourceData = sourceData,
            FailureReason = failureReason,
            CreatedAt = createdAt,
        };
}
