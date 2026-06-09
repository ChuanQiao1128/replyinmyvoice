using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Application.UseCases.Rewrite;
using ReplyInMyVoice.Domain.Contracts;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Infrastructure.Repositories;
using ReplyInMyVoice.Infrastructure.Services;
using AppResultKind = ReplyInMyVoice.Application.Common.ApplicationResultKind;

namespace ReplyInMyVoice.Tests;

public sealed class RetentionServiceTests
{
    [Fact]
    public async Task RetentionScrubsTerminalPayloadsAfterThirtyDaysOnly()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var now = DateTimeOffset.Parse("2026-06-05T00:00:00Z");
        var oldSucceededId = Guid.NewGuid();
        var oldFailedId = Guid.NewGuid();
        var oldExpiredId = Guid.NewGuid();
        var oldPendingId = Guid.NewGuid();
        var oldProcessingId = Guid.NewGuid();
        var freshSucceededId = Guid.NewGuid();

        await using (var seedDb = fixture.CreateContext())
        {
            seedDb.RewriteAttempts.AddRange(
                Attempt(oldSucceededId, user.Id, "old-succeeded", RewriteAttemptStatus.Succeeded, now.AddDays(-31)),
                Attempt(oldFailedId, user.Id, "old-failed", RewriteAttemptStatus.Failed, now.AddDays(-31)),
                Attempt(oldExpiredId, user.Id, "old-expired", RewriteAttemptStatus.Expired, now.AddDays(-31)),
                Attempt(oldPendingId, user.Id, "old-pending", RewriteAttemptStatus.Pending, now.AddDays(-31)),
                Attempt(oldProcessingId, user.Id, "old-processing", RewriteAttemptStatus.Processing, now.AddDays(-31)),
                Attempt(freshSucceededId, user.Id, "fresh-succeeded", RewriteAttemptStatus.Succeeded, now.AddDays(-29)));
            await seedDb.SaveChangesAsync();
        }

        var retention = new RetentionService(fixture.CreateContext);

        var scrubbed = await retention.ScrubExpiredRawContentAsync(now, cancellationToken: CancellationToken.None);

        scrubbed.Should().Be(3);
        await using var verifyDb = fixture.CreateContext();
        (await verifyDb.RewriteAttempts.CountAsync()).Should().Be(6);

        foreach (var attemptId in new[] { oldSucceededId, oldFailedId, oldExpiredId })
        {
            var attempt = await verifyDb.RewriteAttempts.SingleAsync(x => x.Id == attemptId);
            attempt.RequestJson.Should().BeNull();
            attempt.ResultJson.Should().BeNull();
            attempt.Status.Should().BeOneOf(
                RewriteAttemptStatus.Succeeded,
                RewriteAttemptStatus.Failed,
                RewriteAttemptStatus.Expired);
        }

        foreach (var attemptId in new[] { oldPendingId, oldProcessingId, freshSucceededId })
        {
            var attempt = await verifyDb.RewriteAttempts.SingleAsync(x => x.Id == attemptId);
            attempt.RequestJson.Should().Contain("raw draft");
            attempt.ResultJson.Should().Contain("raw result");
        }
    }

    [Fact]
    public async Task RetentionScrubsRawAfterTtl()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var now = DateTimeOffset.Parse("2026-05-30T00:00:00Z");
        var oldAttemptId = Guid.NewGuid();
        var freshAttemptId = Guid.NewGuid();

        await using (var seedDb = fixture.CreateContext())
        {
            seedDb.RewriteAttempts.AddRange(
                new RewriteAttempt
                {
                    Id = oldAttemptId,
                    UserId = user.Id,
                    IdempotencyKey = "old-attempt",
                    RequestHash = "old-hash",
                    RequestJson = "{\"roughDraftReply\":\"old raw draft\"}",
                    ResultJson = "{\"rewrittenText\":\"old raw result\"}",
                    Status = RewriteAttemptStatus.Succeeded,
                    CreatedAt = now.AddDays(-91),
                    CompletedAt = now.AddDays(-91).AddMinutes(1),
                    ExpiresAt = now.AddDays(-91).AddMinutes(15),
                },
                new RewriteAttempt
                {
                    Id = freshAttemptId,
                    UserId = user.Id,
                    IdempotencyKey = "fresh-attempt",
                    RequestHash = "fresh-hash",
                    RequestJson = "{\"roughDraftReply\":\"fresh raw draft\"}",
                    ResultJson = "{\"rewrittenText\":\"fresh raw result\"}",
                    Status = RewriteAttemptStatus.Succeeded,
                    CreatedAt = now.AddDays(-89),
                    CompletedAt = now.AddDays(-89).AddMinutes(1),
                    ExpiresAt = now.AddDays(-89).AddMinutes(15),
                });
            await seedDb.SaveChangesAsync();
        }

        var retention = new RetentionService(fixture.CreateContext);

        var scrubbed = await retention.ScrubExpiredRawContentAsync(now, retentionDays: 90, CancellationToken.None);

        scrubbed.Should().Be(1);
        await using var verifyDb = fixture.CreateContext();
        (await verifyDb.RewriteAttempts.CountAsync()).Should().Be(2);
        var oldAttempt = await verifyDb.RewriteAttempts.SingleAsync(x => x.Id == oldAttemptId);
        oldAttempt.RequestJson.Should().BeNull();
        oldAttempt.ResultJson.Should().BeNull();
        oldAttempt.UserId.Should().Be(user.Id);
        oldAttempt.IdempotencyKey.Should().Be("old-attempt");
        oldAttempt.RequestHash.Should().Be("old-hash");
        oldAttempt.Status.Should().Be(RewriteAttemptStatus.Succeeded);
        oldAttempt.CreatedAt.Should().Be(now.AddDays(-91));
        oldAttempt.CompletedAt.Should().Be(now.AddDays(-91).AddMinutes(1));

        var freshAttempt = await verifyDb.RewriteAttempts.SingleAsync(x => x.Id == freshAttemptId);
        freshAttempt.RequestJson.Should().Contain("fresh raw draft");
        freshAttempt.ResultJson.Should().Contain("fresh raw result");
    }

    [Fact]
    public async Task ConsentPersisted()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var now = DateTimeOffset.Parse("2026-05-30T12:00:00Z");
        await using var handlerDb = fixture.CreateContext();
        var handler = CreateRewriteHandler(handlerDb);

        var result = await handler.HandleAsync(new CreateRewriteAttemptCommand(
            user.Id,
            "consent-idem",
            new RewriteRequest("message", "rough draft reply", "teacher", "reply", "facts", "preserve", "warm"),
            "free:lifetime",
            QuotaLimit: 3,
            now,
            ApiKeyId: null));

        result.Kind.Should().Be(AppResultKind.Created);
        await using var verifyDb = fixture.CreateContext();
        var updatedUser = await verifyDb.AppUsers.SingleAsync(x => x.Id == user.Id);
        updatedUser.ConsentAcceptedAt.Should().Be(now);
        updatedUser.UpdatedAt.Should().Be(now);
    }

    private static RewriteAttempt Attempt(
        Guid id,
        Guid userId,
        string key,
        RewriteAttemptStatus status,
        DateTimeOffset createdAt)
    {
        return new RewriteAttempt
        {
            Id = id,
            UserId = userId,
            IdempotencyKey = key,
            RequestHash = $"{key}-hash",
            RequestJson = $"{{\"roughDraftReply\":\"{key} raw draft\"}}",
            ResultJson = $"{{\"rewrittenText\":\"{key} raw result\"}}",
            Status = status,
            CreatedAt = createdAt,
            CompletedAt = status is RewriteAttemptStatus.Succeeded or RewriteAttemptStatus.Failed or RewriteAttemptStatus.Expired
                ? createdAt.AddMinutes(1)
                : null,
            ExpiresAt = createdAt.AddMinutes(15),
        };
    }

    private static CreateRewriteAttemptHandler CreateRewriteHandler(ReplyInMyVoice.Infrastructure.Data.AppDbContext db) =>
        new(
            new AppUserRepository(db),
            new UsagePeriodRepository(db),
            new RewriteAttemptRepository(db),
            new UsageReservationRepository(db),
            new RewriteCreditRepository(db),
            new OutboxMessageRepository(db),
            new UnitOfWork(db));
}
