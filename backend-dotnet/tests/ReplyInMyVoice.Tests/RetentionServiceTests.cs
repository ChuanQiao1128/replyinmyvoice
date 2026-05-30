using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Domain.Contracts;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Infrastructure.Services;

namespace ReplyInMyVoice.Tests;

public sealed class RetentionServiceTests
{
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
        var service = new RewriteRequestService(
            fixture.CreateContext,
            new QuotaService(fixture.CreateContext));
        var now = DateTimeOffset.Parse("2026-05-30T12:00:00Z");

        var result = await service.CreateAttemptAsync(
            user.Id,
            "consent-idem",
            new RewriteRequest("message", "rough draft reply", "teacher", "reply", "facts", "preserve", "warm"),
            "free:lifetime",
            quotaLimit: 3,
            now,
            CancellationToken.None);

        result.Kind.Should().Be(ReserveRewriteResultKind.Created);
        await using var verifyDb = fixture.CreateContext();
        var updatedUser = await verifyDb.AppUsers.SingleAsync(x => x.Id == user.Id);
        updatedUser.ConsentAcceptedAt.Should().Be(now);
        updatedUser.UpdatedAt.Should().Be(now);
    }
}
