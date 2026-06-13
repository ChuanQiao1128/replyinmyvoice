using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ReplyInMyVoice.Application.UseCases.Rewrite;
using ReplyInMyVoice.Domain.Contracts;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Functions.Functions;
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
    public async Task RetentionScrubsSoftDeletedAttempts()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var now = DateTimeOffset.Parse("2026-06-05T00:00:00Z");
        var attemptId = Guid.NewGuid();

        await using (var seedDb = fixture.CreateContext())
        {
            seedDb.RewriteAttempts.Add(Attempt(
                attemptId,
                user.Id,
                "old-soft-deleted",
                RewriteAttemptStatus.Succeeded,
                now.AddDays(-31),
                deletedAt: now.AddDays(-1)));
            await seedDb.SaveChangesAsync();
        }

        var retention = new RetentionService(fixture.CreateContext);

        var scrubbed = await retention.ScrubExpiredRawContentAsync(now, cancellationToken: CancellationToken.None);

        scrubbed.Should().Be(1);
        await using var verifyDb = fixture.CreateContext();
        var attempt = await verifyDb.RewriteAttempts
            .IgnoreQueryFilters()
            .SingleAsync(x => x.Id == attemptId);
        attempt.RequestJson.Should().BeNull();
        attempt.ResultJson.Should().BeNull();
        attempt.DeletedAt.Should().NotBeNull();
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
    public async Task SandboxPurgeDeletesOnlyOldTestKeyAttempts()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var now = DateTimeOffset.Parse("2026-06-13T00:00:00Z");
        var testKeyId = Guid.NewGuid();
        var liveKeyId = Guid.NewGuid();
        var oldSandboxId = Guid.NewGuid();
        var oldSoftDeletedSandboxId = Guid.NewGuid();
        var freshSandboxId = Guid.NewGuid();
        var oldLiveKeyId = Guid.NewGuid();
        var oldLiveKeyPrefixedId = Guid.NewGuid();

        await using (var seedDb = fixture.CreateContext())
        {
            seedDb.ApiKeys.AddRange(
                ApiKey(testKeyId, user.Id, isTest: true),
                ApiKey(liveKeyId, user.Id, isTest: false));
            seedDb.RewriteAttempts.AddRange(
                Attempt(
                    oldSandboxId,
                    user.Id,
                    $"{SandboxAttemptConventions.IdempotencyKeyPrefix}old-test",
                    RewriteAttemptStatus.Succeeded,
                    now.AddDays(-8),
                    apiKeyId: testKeyId),
                Attempt(
                    oldSoftDeletedSandboxId,
                    user.Id,
                    $"{SandboxAttemptConventions.IdempotencyKeyPrefix}old-soft-test",
                    RewriteAttemptStatus.Succeeded,
                    now.AddDays(-8),
                    apiKeyId: testKeyId,
                    deletedAt: now.AddDays(-7)),
                Attempt(
                    freshSandboxId,
                    user.Id,
                    $"{SandboxAttemptConventions.IdempotencyKeyPrefix}fresh-test",
                    RewriteAttemptStatus.Succeeded,
                    now.AddDays(-2),
                    apiKeyId: testKeyId),
                Attempt(
                    oldLiveKeyId,
                    user.Id,
                    "old-live-key",
                    RewriteAttemptStatus.Succeeded,
                    now.AddDays(-40),
                    apiKeyId: liveKeyId),
                Attempt(
                    oldLiveKeyPrefixedId,
                    user.Id,
                    $"{SandboxAttemptConventions.IdempotencyKeyPrefix}old-live-prefix",
                    RewriteAttemptStatus.Succeeded,
                    now.AddDays(-8),
                    apiKeyId: liveKeyId));
            await seedDb.SaveChangesAsync();
        }

        var retention = new RetentionService(fixture.CreateContext);

        var purged = await retention.PurgeExpiredSandboxAttemptsAsync(
            now,
            cancellationToken: CancellationToken.None);

        purged.Should().Be(2);
        await using var verifyDb = fixture.CreateContext();
        var remaining = await verifyDb.RewriteAttempts
            .IgnoreQueryFilters()
            .ToDictionaryAsync(x => x.Id);
        remaining.Keys.Should().BeEquivalentTo(new[] { freshSandboxId, oldLiveKeyId, oldLiveKeyPrefixedId });
        remaining[freshSandboxId].ApiKeyId.Should().Be(testKeyId);
        remaining[oldLiveKeyId].ApiKeyId.Should().Be(liveKeyId);
        remaining[oldLiveKeyId].RequestJson.Should().Contain("old-live-key raw draft");
        remaining[oldLiveKeyPrefixedId].ApiKeyId.Should().Be(liveKeyId);
    }

    [Fact]
    public async Task SandboxPurgeRejectsNonPositiveRetention()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var retention = new RetentionService(fixture.CreateContext);

        Func<Task> act = () => retention.PurgeExpiredSandboxAttemptsAsync(
            DateTimeOffset.Parse("2026-06-13T00:00:00Z"),
            sandboxRetentionDays: 0,
            cancellationToken: CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task RetentionPurgeFunctionRunsScrubAndSandboxPurge()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var now = DateTimeOffset.UtcNow;
        var oldPayloadId = Guid.NewGuid();
        var oldSandboxId = Guid.NewGuid();
        var testKeyId = Guid.NewGuid();

        await using (var seedDb = fixture.CreateContext())
        {
            seedDb.ApiKeys.Add(ApiKey(testKeyId, user.Id, isTest: true));
            seedDb.RewriteAttempts.AddRange(
                Attempt(
                    oldPayloadId,
                    user.Id,
                    "timer-old-payload",
                    RewriteAttemptStatus.Succeeded,
                    now.AddDays(-31)),
                Attempt(
                    oldSandboxId,
                    user.Id,
                    $"{SandboxAttemptConventions.IdempotencyKeyPrefix}timer-old-sandbox",
                    RewriteAttemptStatus.Succeeded,
                    now.AddDays(-8),
                    apiKeyId: testKeyId));
            await seedDb.SaveChangesAsync();
        }

        var function = new RetentionPurgeFunction(
            new RetentionService(fixture.CreateContext),
            NullLogger<RetentionPurgeFunction>.Instance);

        await function.Run(null!, CancellationToken.None);

        await using var verifyDb = fixture.CreateContext();
        var payload = await verifyDb.RewriteAttempts.SingleAsync(x => x.Id == oldPayloadId);
        payload.RequestJson.Should().BeNull();
        payload.ResultJson.Should().BeNull();
        (await verifyDb.RewriteAttempts
            .IgnoreQueryFilters()
            .AnyAsync(x => x.Id == oldSandboxId))
            .Should()
            .BeFalse();
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
        DateTimeOffset createdAt,
        Guid? apiKeyId = null,
        DateTimeOffset? deletedAt = null)
    {
        return new RewriteAttempt
        {
            Id = id,
            UserId = userId,
            ApiKeyId = apiKeyId,
            IdempotencyKey = key,
            RequestHash = $"{key}-hash",
            RequestJson = $"{{\"roughDraftReply\":\"{key} raw draft\"}}",
            ResultJson = $"{{\"rewrittenText\":\"{key} raw result\"}}",
            Status = status,
            CreatedAt = createdAt,
            CompletedAt = status is RewriteAttemptStatus.Succeeded or RewriteAttemptStatus.Failed or RewriteAttemptStatus.Expired
                ? createdAt.AddMinutes(1)
                : null,
            DeletedAt = deletedAt,
            ExpiresAt = createdAt.AddMinutes(15),
        };
    }

    private static ApiKey ApiKey(
        Guid id,
        Guid userId,
        bool isTest) =>
        new()
        {
            Id = id,
            UserId = userId,
            KeyHash = $"{id:N}-hash",
            Last4 = "abcd",
            Name = isTest ? "Test key" : "Live key",
            IsTest = isTest,
            CreatedAt = DateTimeOffset.Parse("2026-06-01T00:00:00Z"),
            UpdatedAt = DateTimeOffset.Parse("2026-06-01T00:00:00Z"),
        };

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
