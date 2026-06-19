using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Application.Common;
using ReplyInMyVoice.Application.UseCases.Rewrite;
using ReplyInMyVoice.Domain.Contracts;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Infrastructure.Repositories;

namespace ReplyInMyVoice.Tests.HandlerAcceptance.CreateRewriteAttemptHandler;

public sealed class ExpiredCreditsTests
{
    [Fact]
    public async Task CreateRewriteAttemptHandler_returns_credits_expired_when_only_expired_credits_have_remaining_balance()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var now = DateTimeOffset.Parse("2026-06-19T00:00:00Z");
        await SeedExpiredCreditAsync(fixture, user.Id, now, amountGranted: 5, amountConsumed: 0);
        await using var handlerDb = fixture.CreateContext();
        var handler = CreateHandler(handlerDb);

        var result = await handler.HandleAsync(new CreateRewriteAttemptCommand(
            user.Id,
            "idem-expired-credit-only",
            Request("Please send a clear update."),
            "free:lifetime",
            QuotaLimit: 0,
            Now: now,
            ApiKeyId: null));

        result.Kind.Should().Be(ApplicationResultKind.QuotaExceeded);
        result.ErrorCode.Should().Be(RewriteEngineErrorCodes.CreditsExpired);
        result.Value.Should().BeNull();

        await using var verifyDb = fixture.CreateContext();
        var credit = await verifyDb.RewriteCredits.SingleAsync();
        credit.AmountConsumed.Should().Be(0);
        (await verifyDb.UsagePeriods.CountAsync()).Should().Be(0);
        (await verifyDb.RewriteAttempts.CountAsync()).Should().Be(0);
        (await verifyDb.UsageReservations.CountAsync()).Should().Be(0);
        (await verifyDb.OutboxMessages.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task CreateRewriteAttemptHandler_reserves_period_slot_when_period_quota_is_available_even_with_expired_credits()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var now = DateTimeOffset.Parse("2026-06-19T00:00:00Z");
        await SeedExpiredCreditAsync(fixture, user.Id, now, amountGranted: 5, amountConsumed: 0);
        await using var handlerDb = fixture.CreateContext();
        var handler = CreateHandler(handlerDb);

        var result = await handler.HandleAsync(new CreateRewriteAttemptCommand(
            user.Id,
            "idem-period-before-expired-credit",
            Request("Please send a clear update."),
            "free:lifetime",
            QuotaLimit: 3,
            Now: now,
            ApiKeyId: null));

        result.Kind.Should().Be(ApplicationResultKind.Created);
        result.ErrorCode.Should().BeNull();

        await using var verifyDb = fixture.CreateContext();
        var period = await verifyDb.UsagePeriods.SingleAsync();
        period.ReservedCount.Should().Be(1);
        var credit = await verifyDb.RewriteCredits.SingleAsync();
        credit.AmountConsumed.Should().Be(0);
        var reservation = await verifyDb.UsageReservations.SingleAsync();
        reservation.RewriteCreditId.Should().BeNull();
        (await verifyDb.OutboxMessages.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task CreateRewriteAttemptHandler_returns_generic_quota_exceeded_when_no_period_quota_and_no_expired_credits()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var now = DateTimeOffset.Parse("2026-06-19T00:00:00Z");
        await using var handlerDb = fixture.CreateContext();
        var handler = CreateHandler(handlerDb);

        var result = await handler.HandleAsync(new CreateRewriteAttemptCommand(
            user.Id,
            "idem-no-quota-no-expired-credit",
            Request("Please send a clear update."),
            "free:lifetime",
            QuotaLimit: 0,
            Now: now,
            ApiKeyId: null));

        result.Kind.Should().Be(ApplicationResultKind.QuotaExceeded);
        result.ErrorCode.Should().BeNull();
        result.Value.Should().BeNull();

        await using var verifyDb = fixture.CreateContext();
        (await verifyDb.UsagePeriods.CountAsync()).Should().Be(0);
        (await verifyDb.RewriteAttempts.CountAsync()).Should().Be(0);
        (await verifyDb.UsageReservations.CountAsync()).Should().Be(0);
        (await verifyDb.OutboxMessages.CountAsync()).Should().Be(0);
    }

    [Fact]
    public void CreditsExpired_is_not_a_quality_gate_or_engine_emittable_code()
    {
        RewriteEngineErrorCodes.QualityGateNotCharged.Should().NotContain(RewriteEngineErrorCodes.CreditsExpired);
        RewriteEngineErrorCodes.EngineEmittable.Should().NotContain(RewriteEngineErrorCodes.CreditsExpired);
    }

    private static async Task SeedExpiredCreditAsync(
        DbFixture fixture,
        Guid userId,
        DateTimeOffset now,
        int amountGranted,
        int amountConsumed)
    {
        await using var db = fixture.CreateContext();
        db.RewriteCredits.Add(new RewriteCredit
        {
            UserId = userId,
            Source = "PROMO",
            AmountGranted = amountGranted,
            AmountConsumed = amountConsumed,
            GrantedAt = now.AddDays(-10),
            ExpiresAt = now,
        });
        await db.SaveChangesAsync();
    }

    private static ReplyInMyVoice.Application.UseCases.Rewrite.CreateRewriteAttemptHandler CreateHandler(
        ReplyInMyVoice.Infrastructure.Data.AppDbContext db) =>
        new(
            new AppUserRepository(db),
            new UsagePeriodRepository(db),
            new RewriteAttemptRepository(db),
            new UsageReservationRepository(db),
            new RewriteCreditRepository(db),
            new OutboxMessageRepository(db),
            new UnitOfWork(db),
            new NoopOutboxFastPathDispatcher());

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
