using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.Common;
using ReplyInMyVoice.Application.UseCases.Rewrite;
using ReplyInMyVoice.Domain.Contracts;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Infrastructure.Repositories;

namespace ReplyInMyVoice.Tests.Application;

public sealed class RewriteUseCaseTests
{
    [Fact]
    public async Task CreateRewriteAttemptAsync_reserves_period_quota_and_writes_job_outbox()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var now = DateTimeOffset.Parse("2026-06-09T00:00:00Z");
        await using var handlerDb = fixture.CreateContext();
        var handler = CreateHandler(handlerDb);

        var result = await handler.HandleAsync(new CreateRewriteAttemptCommand(
            user.Id,
            "idem-create",
            Request("Please reply soon."),
            "free:lifetime",
            QuotaLimit: 3,
            Now: now,
            ApiKeyId: null));

        result.Kind.Should().Be(ApplicationResultKind.Created);
        result.Value.Should().NotBeNull();
        result.Value!.Status.Should().Be(RewriteAttemptStatus.Pending.ToString());

        await using var verifyDb = fixture.CreateContext();
        var attempt = await verifyDb.RewriteAttempts.SingleAsync();
        attempt.Id.Should().Be(result.Value.AttemptId);
        attempt.UserId.Should().Be(user.Id);
        attempt.Status.Should().Be(RewriteAttemptStatus.Pending);
        attempt.RequestJson.Should().Contain("Please reply soon.");
        attempt.ExpiresAt.Should().Be(now.AddMinutes(15));

        var period = await verifyDb.UsagePeriods.SingleAsync();
        period.UsedCount.Should().Be(0);
        period.ReservedCount.Should().Be(1);

        var reservation = await verifyDb.UsageReservations.SingleAsync();
        reservation.Status.Should().Be(UsageReservationStatus.Pending);
        reservation.RewriteCreditId.Should().BeNull();

        var outbox = await verifyDb.OutboxMessages.SingleAsync();
        outbox.Status.Should().Be(OutboxMessageStatus.Pending);
        outbox.MessageType.Should().Be("RewriteJobCreated");
        outbox.CorrelationId.Should().Be(attempt.Id.ToString());
        outbox.PayloadJson.Should().Contain(attempt.Id.ToString());
    }

    [Fact]
    public async Task CreateRewriteAttemptAsync_returns_existing_for_same_idempotency_key_and_request()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        await using var handlerDb = fixture.CreateContext();
        var handler = CreateHandler(handlerDb);
        var request = Request("Thanks for checking in.");
        var now = DateTimeOffset.Parse("2026-06-09T00:00:00Z");

        var first = await handler.HandleAsync(new CreateRewriteAttemptCommand(
            user.Id,
            "idem-repeat",
            request,
            "free:lifetime",
            3,
            now,
            ApiKeyId: null));
        var second = await handler.HandleAsync(new CreateRewriteAttemptCommand(
            user.Id,
            "idem-repeat",
            request,
            "free:lifetime",
            3,
            now.AddSeconds(1),
            ApiKeyId: null));

        second.Kind.Should().Be(ApplicationResultKind.Existing);
        second.Value!.AttemptId.Should().Be(first.Value!.AttemptId);

        await using var verifyDb = fixture.CreateContext();
        (await verifyDb.RewriteAttempts.CountAsync()).Should().Be(1);
        (await verifyDb.UsageReservations.CountAsync()).Should().Be(1);
        (await verifyDb.OutboxMessages.CountAsync()).Should().Be(1);
        (await verifyDb.UsagePeriods.SingleAsync()).ReservedCount.Should().Be(1);
    }

    [Fact]
    public async Task CreateRewriteAttemptAsync_rejects_reused_idempotency_key_with_different_request()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        await using var handlerDb = fixture.CreateContext();
        var handler = CreateHandler(handlerDb);
        var now = DateTimeOffset.Parse("2026-06-09T00:00:00Z");

        var first = await handler.HandleAsync(new CreateRewriteAttemptCommand(
            user.Id,
            "idem-conflict",
            Request("First reply."),
            "free:lifetime",
            3,
            now,
            ApiKeyId: null));
        var second = await handler.HandleAsync(new CreateRewriteAttemptCommand(
            user.Id,
            "idem-conflict",
            Request("Different reply."),
            "free:lifetime",
            3,
            now,
            ApiKeyId: null));

        second.Kind.Should().Be(ApplicationResultKind.Conflict);
        second.Value!.AttemptId.Should().Be(first.Value!.AttemptId);

        await using var verifyDb = fixture.CreateContext();
        (await verifyDb.RewriteAttempts.CountAsync()).Should().Be(1);
        (await verifyDb.UsageReservations.CountAsync()).Should().Be(1);
        (await verifyDb.OutboxMessages.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task CreateRewriteAttemptAsync_blocks_suspended_user_without_side_effects()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        await using (var seedDb = fixture.CreateContext())
        {
            var suspendedUser = await seedDb.AppUsers.SingleAsync(x => x.Id == user.Id);
            suspendedUser.SuspendedAt = DateTimeOffset.Parse("2026-06-09T00:00:00Z");
            await seedDb.SaveChangesAsync();
        }

        await using var handlerDb = fixture.CreateContext();
        var handler = CreateHandler(handlerDb);

        var result = await handler.HandleAsync(new CreateRewriteAttemptCommand(
            user.Id,
            "idem-suspended",
            Request("Please write back."),
            "free:lifetime",
            3,
            DateTimeOffset.Parse("2026-06-09T00:00:00Z"),
            ApiKeyId: null));

        result.Kind.Should().Be(ApplicationResultKind.QuotaExceeded);
        result.ErrorCode.Should().Be("user_suspended");

        await using var verifyDb = fixture.CreateContext();
        (await verifyDb.RewriteAttempts.CountAsync()).Should().Be(0);
        (await verifyDb.UsageReservations.CountAsync()).Should().Be(0);
        (await verifyDb.OutboxMessages.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task CreateRewriteAttemptAsync_leaves_no_pending_period_mutation_when_quota_is_exhausted()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var now = DateTimeOffset.Parse("2026-06-09T00:00:00Z");
        await using var handlerDb = fixture.CreateContext();
        var handler = CreateHandler(handlerDb);

        var result = await handler.HandleAsync(new CreateRewriteAttemptCommand(
            user.Id,
            "idem-no-quota",
            Request("Please confirm next steps."),
            "free:lifetime",
            QuotaLimit: 0,
            Now: now,
            ApiKeyId: null));

        result.Kind.Should().Be(ApplicationResultKind.QuotaExceeded);

        await handlerDb.SaveChangesAsync();
        await using var verifyDb = fixture.CreateContext();
        (await verifyDb.UsagePeriods.CountAsync()).Should().Be(0);
        (await verifyDb.RewriteAttempts.CountAsync()).Should().Be(0);
        (await verifyDb.UsageReservations.CountAsync()).Should().Be(0);
        (await verifyDb.OutboxMessages.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task CreateRewriteAttemptAsync_consumes_available_credit_when_period_quota_is_exhausted()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var now = DateTimeOffset.Parse("2026-06-09T00:00:00Z");
        await using (var seedDb = fixture.CreateContext())
        {
            seedDb.RewriteCredits.Add(new RewriteCredit
            {
                UserId = user.Id,
                Source = "PROMO",
                AmountGranted = 1,
                AmountConsumed = 0,
                GrantedAt = now.AddDays(-1),
                ExpiresAt = now.AddDays(1),
            });
            await seedDb.SaveChangesAsync();
        }

        await using var handlerDb = fixture.CreateContext();
        var handler = CreateHandler(handlerDb);

        var result = await handler.HandleAsync(new CreateRewriteAttemptCommand(
            user.Id,
            "idem-credit",
            Request("Please confirm next steps."),
            "free:lifetime",
            QuotaLimit: 0,
            Now: now,
            ApiKeyId: null));

        result.Kind.Should().Be(ApplicationResultKind.Created);

        await using var verifyDb = fixture.CreateContext();
        var period = await verifyDb.UsagePeriods.SingleAsync();
        period.ReservedCount.Should().Be(0);
        var credit = await verifyDb.RewriteCredits.SingleAsync();
        credit.AmountConsumed.Should().Be(1);
        var reservation = await verifyDb.UsageReservations.SingleAsync();
        reservation.RewriteCreditId.Should().Be(credit.Id);
        (await verifyDb.OutboxMessages.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task GetRewriteAttemptAsync_returns_only_attempt_owned_by_user()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var owner = await fixture.CreateUserAsync();
        var otherUser = await fixture.CreateUserAsync();
        var now = DateTimeOffset.Parse("2026-06-09T00:00:00Z");
        Guid attemptId;
        await using (var seedDb = fixture.CreateContext())
        {
            var attempt = new RewriteAttempt
            {
                UserId = owner.Id,
                IdempotencyKey = "get-owned",
                RequestHash = "hash-owned",
                RequestJson = "{\"roughDraftReply\":\"Thanks for the update.\"}",
                Status = RewriteAttemptStatus.Succeeded,
                ResultJson = "{\"rewrittenText\":\"Thanks for the update.\"}",
                CreatedAt = now,
                CompletedAt = now.AddMinutes(1),
                ExpiresAt = now.AddMinutes(15),
            };
            seedDb.RewriteAttempts.Add(attempt);
            await seedDb.SaveChangesAsync();
            attemptId = attempt.Id;
        }

        await using var handlerDb = fixture.CreateContext();
        var handler = new GetRewriteAttemptHandler(new RewriteAttemptRepository(handlerDb));

        var owned = await handler.HandleAsync(new GetRewriteAttemptQuery(attemptId, owner.Id));
        var other = await handler.HandleAsync(new GetRewriteAttemptQuery(attemptId, otherUser.Id));

        owned.Kind.Should().Be(ApplicationResultKind.Success);
        owned.Value!.AttemptId.Should().Be(attemptId);
        owned.Value.ResultJson.Should().Contain("rewrittenText");
        other.Kind.Should().Be(ApplicationResultKind.NotFound);
        other.Value.Should().BeNull();
    }

    private static CreateRewriteAttemptHandler CreateHandler(ReplyInMyVoice.Infrastructure.Data.AppDbContext db) =>
        new(
            new AppUserRepository(db),
            new UsagePeriodRepository(db),
            new RewriteAttemptRepository(db),
            new UsageReservationRepository(db),
            new RewriteCreditRepository(db),
            new OutboxMessageRepository(db),
            new UnitOfWork(db),
            new ReplyInMyVoice.Tests.NoopOutboxFastPathDispatcher());

    private static RewriteRequest Request(string roughDraftReply) =>
        new(
            MessageToReplyTo: "Can you send an update today?",
            RoughDraftReply: roughDraftReply,
            Audience: "client",
            Purpose: "reply",
            WhatHappened: "The update is ready.",
            FactsToPreserve: "No dates changed.",
            Tone: "warm");
}
