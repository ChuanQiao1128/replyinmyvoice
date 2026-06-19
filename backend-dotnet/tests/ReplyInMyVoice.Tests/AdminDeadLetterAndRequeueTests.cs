using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ReplyInMyVoice.Application.UseCases.Admin;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Functions.Functions;
using ReplyInMyVoice.Infrastructure.Data;
using ReplyInMyVoice.Infrastructure.Repositories;
using System.Security.Claims;

namespace ReplyInMyVoice.Tests;

public sealed class AdminDeadLetterAndRequeueTests
{
    [Fact]
    public async Task Dead_letter_model_is_configured()
    {
        await using var fixture = await DbFixture.CreateAsync();
        await using var db = fixture.CreateContext();

        var entity = db.Model.FindEntityType("ReplyInMyVoice.Domain.Entities.DeadLetterMessage");

        entity.Should().NotBeNull();
        entity!.FindPrimaryKey()!.Properties.Select(x => x.Name).Should().ContainSingle("Id");
        var indexColumns = entity.GetIndexes()
            .Select(index => string.Join(",", index.Properties.Select(x => x.Name)))
            .ToList();
        indexColumns.Should().Contain("EntityType,CreatedAt");
        indexColumns.Should().Contain("OutboxMessageId");
        indexColumns.Should().Contain("StripeEventId");
    }

    [Fact]
    public async Task Outbox_Failed_can_be_requeued_to_Pending()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var now = DateTimeOffset.Parse("2026-06-19T10:00:00Z");
        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            MessageType = "test.outbox",
            PayloadJson = "{\"ok\":true}",
            Status = OutboxMessageStatus.Failed,
            CreatedAt = now.AddHours(-2),
            NextAttemptAt = now.AddHours(1),
            AttemptCount = 5,
            MaxAttempts = 5,
            LockedBy = "worker-a",
            LockedUntil = now.AddMinutes(5),
            LastAttemptAt = now.AddMinutes(-5),
            LastError = "terminal failure",
        };
        var originalRowVersion = message.RowVersion;

        await using (var seedDb = fixture.CreateContext())
        {
            seedDb.OutboxMessages.Add(message);
            seedDb.DeadLetterMessages.Add(CreateDeadLetter(
                DeadLetterEntityType.Outbox,
                message.Id.ToString("D"),
                now.AddMinutes(-4),
                outboxMessageId: message.Id));
            await seedDb.SaveChangesAsync();
        }

        await using (var requeueDb = fixture.CreateContext())
        {
            var handler = CreateOutboxHandler(requeueDb);
            var result = await handler.HandleAsync(new RequeueFailedOutboxMessageCommand(
                message.Id,
                "admin-owner-oid",
                now));

            result.Kind.Should().Be(AdminDeadLetterRequeueResultKind.Success);
        }

        await using var verifyDb = fixture.CreateContext();
        var stored = await verifyDb.OutboxMessages.SingleAsync(x => x.Id == message.Id);
        stored.Status.Should().Be(OutboxMessageStatus.Pending);
        stored.AttemptCount.Should().Be(0);
        stored.NextAttemptAt.Should().Be(now);
        stored.LastError.Should().BeNull();
        stored.LockedBy.Should().BeNull();
        stored.LockedUntil.Should().BeNull();
        stored.RowVersion.Should().Be(originalRowVersion);
        (await verifyDb.DeadLetterMessages.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task StripeEvent_Failed_can_be_requeued_to_Pending()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var now = DateTimeOffset.Parse("2026-06-19T10:15:00Z");
        const string payload = "{\"id\":\"evt_requeue_failed\"}";
        var stripeEvent = new StripeEvent
        {
            EventId = "evt_requeue_failed",
            Type = "checkout.session.completed",
            Status = StripeEventStatus.Failed,
            AttemptCount = 8,
            LastError = "terminal Stripe failure",
            PayloadJson = payload,
            LastAttemptAt = now.AddMinutes(-2),
            LockedUntil = now.AddMinutes(5),
            CreatedAt = now.AddHours(-1),
        };
        var originalRowVersion = stripeEvent.RowVersion;

        await using (var seedDb = fixture.CreateContext())
        {
            seedDb.StripeEvents.Add(stripeEvent);
            seedDb.DeadLetterMessages.Add(CreateDeadLetter(
                DeadLetterEntityType.Stripe,
                stripeEvent.EventId,
                now.AddMinutes(-1),
                stripeEventId: stripeEvent.EventId));
            await seedDb.SaveChangesAsync();
        }

        await using (var requeueDb = fixture.CreateContext())
        {
            var handler = CreateStripeHandler(requeueDb);
            var result = await handler.HandleAsync(new RequeueFailedStripeEventCommand(
                stripeEvent.EventId,
                now));

            result.Kind.Should().Be(AdminDeadLetterRequeueResultKind.Success);
        }

        await using var verifyDb = fixture.CreateContext();
        var stored = await verifyDb.StripeEvents.SingleAsync(x => x.EventId == stripeEvent.EventId);
        stored.Status.Should().Be(StripeEventStatus.Pending);
        stored.AttemptCount.Should().Be(0);
        stored.LastError.Should().BeNull();
        stored.LockedUntil.Should().BeNull();
        stored.PayloadJson.Should().Be(payload);
        stored.RowVersion.Should().Be(originalRowVersion);
        (await verifyDb.DeadLetterMessages.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Requeue_idempotent()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var now = DateTimeOffset.Parse("2026-06-19T10:30:00Z");
        var later = now.AddMinutes(10);
        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            MessageType = "test.outbox",
            PayloadJson = "{\"ok\":true}",
            Status = OutboxMessageStatus.Failed,
            CreatedAt = now.AddHours(-2),
            NextAttemptAt = now.AddHours(1),
            AttemptCount = 10,
            MaxAttempts = 10,
            LastError = "terminal failure",
        };

        await using (var seedDb = fixture.CreateContext())
        {
            seedDb.OutboxMessages.Add(message);
            seedDb.DeadLetterMessages.Add(CreateDeadLetter(
                DeadLetterEntityType.Outbox,
                message.Id.ToString("D"),
                now.AddMinutes(-1),
                outboxMessageId: message.Id));
            await seedDb.SaveChangesAsync();
        }

        await using (var firstDb = fixture.CreateContext())
        {
            var first = await CreateOutboxHandler(firstDb).HandleAsync(new RequeueFailedOutboxMessageCommand(
                message.Id,
                "admin-owner-oid",
                now));

            first.Kind.Should().Be(AdminDeadLetterRequeueResultKind.Success);
        }

        await using (var secondDb = fixture.CreateContext())
        {
            var second = await CreateOutboxHandler(secondDb).HandleAsync(new RequeueFailedOutboxMessageCommand(
                message.Id,
                "admin-owner-oid",
                later));

            second.Kind.Should().Be(AdminDeadLetterRequeueResultKind.Success);
        }

        await using var verifyDb = fixture.CreateContext();
        var stored = await verifyDb.OutboxMessages.SingleAsync(x => x.Id == message.Id);
        stored.Status.Should().Be(OutboxMessageStatus.Pending);
        stored.AttemptCount.Should().Be(0);
        stored.NextAttemptAt.Should().Be(now);
        stored.LastError.Should().BeNull();
        (await verifyDb.DeadLetterMessages.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Requeue_non_existent_returns_404()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var function = AdminHttpFunctionsTestFactory.Create(BuildConfiguration(), fixture.CreateContext);

        var result = await function.RequeueOutboxMessage(
            CreateRequest("admin-owner-oid", "owner@example.com"),
            Guid.NewGuid().ToString("D"),
            CancellationToken.None);

        var response = result.Should().BeOfType<ObjectResult>().Subject;
        response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Non_admin_blocked_from_requeue()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var function = AdminHttpFunctionsTestFactory.Create(BuildConfiguration(), fixture.CreateContext);

        var result = await function.RequeueOutboxMessage(
            CreateRequest("regular-user-oid", "regular@example.com"),
            "not-a-guid",
            CancellationToken.None);

        var response = result.Should().BeOfType<ObjectResult>().Subject;
        response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task List_dead_letter_with_pagination()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var now = DateTimeOffset.Parse("2026-06-19T11:00:00Z");

        await using (var seedDb = fixture.CreateContext())
        {
            var olderOutboxId = Guid.NewGuid();
            var newerOutboxId = Guid.NewGuid();
            seedDb.DeadLetterMessages.AddRange(
                CreateDeadLetter(
                    DeadLetterEntityType.Outbox,
                    olderOutboxId.ToString("D"),
                    now.AddMinutes(-10)),
                CreateDeadLetter(
                    DeadLetterEntityType.Stripe,
                    "evt_list_stripe",
                    now.AddMinutes(-5)),
                CreateDeadLetter(
                    DeadLetterEntityType.Outbox,
                    newerOutboxId.ToString("D"),
                    now.AddMinutes(-1)));
            await seedDb.SaveChangesAsync();
        }

        var function = AdminHttpFunctionsTestFactory.Create(BuildConfiguration(), fixture.CreateContext);

        var outboxResult = await function.ListDeadLetter(
            CreateRequest("admin-owner-oid", "owner@example.com", "?entityType=outbox&page=1&pageSize=1"),
            CancellationToken.None);

        var outboxPage = outboxResult.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<AdminDeadLetterListResponse>().Subject;
        outboxPage.TotalCount.Should().Be(2);
        outboxPage.TotalPages.Should().Be(2);
        outboxPage.Items.Should().ContainSingle();
        outboxPage.Items[0].EntityType.Should().Be("Outbox");
        outboxPage.Items[0].FailureReason.Should().Be("failure Outbox");

        var stripeResult = await function.ListDeadLetter(
            CreateRequest("admin-owner-oid", "owner@example.com", "?entityType=stripe&page=1&pageSize=10"),
            CancellationToken.None);

        var stripePage = stripeResult.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<AdminDeadLetterListResponse>().Subject;
        stripePage.TotalCount.Should().Be(1);
        stripePage.Items.Should().ContainSingle(item => item.EntityType == "Stripe" && item.EntityId == "evt_list_stripe");
    }

    private static RequeueFailedOutboxMessageHandler CreateOutboxHandler(AppDbContext db) =>
        new(
            new OutboxMessageRepository(db),
            new DeadLetterRepository(db),
            new UnitOfWork(db));

    private static RequeueFailedStripeEventHandler CreateStripeHandler(AppDbContext db) =>
        new(
            new StripeEventRepository(db),
            new DeadLetterRepository(db),
            new UnitOfWork(db));

    private static DeadLetterMessage CreateDeadLetter(
        DeadLetterEntityType entityType,
        string entityId,
        DateTimeOffset createdAt,
        Guid? outboxMessageId = null,
        string? stripeEventId = null) =>
        new()
        {
            EntityType = entityType,
            EntityId = entityId,
            OutboxMessageId = outboxMessageId,
            StripeEventId = stripeEventId,
            FailureReason = $"failure {entityType}",
            FailureCount = 1,
            FirstFailedAt = createdAt,
            LastFailedAt = createdAt,
            CreatedAt = createdAt,
            UpdatedAt = createdAt,
        };

    private static HttpRequest CreateRequest(
        string oid,
        string email,
        string? queryString = null)
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("oid", oid),
                new Claim("email", email),
            ], "Bearer")),
        };

        if (!string.IsNullOrWhiteSpace(queryString))
        {
            context.Request.QueryString = new QueryString(queryString);
        }

        return context.Request;
    }

    private static IConfiguration BuildConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ADMIN_EMAILS"] = "admin-owner-oid,owner@example.com",
            })
            .Build();
}
