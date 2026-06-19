using FluentAssertions;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Infrastructure.Repositories;

namespace ReplyInMyVoice.Tests.Application;

public sealed class WebhookDeliveryFailureMetricsTests
{
    [Fact]
    public async Task GetFailureMetricsAsync_computes()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var otherUser = await fixture.CreateUserAsync();
        var now = DateTimeOffset.Parse("2026-06-18T12:00:00Z");
        Guid apiKeyId;

        await using (var db = fixture.CreateContext())
        {
            var apiKey = AddApiKey(db, user.Id, "owner");
            var otherApiKey = AddApiKey(db, otherUser.Id, "other");
            apiKeyId = apiKey.Id;

            AddDelivery(db, apiKey, WebhookDeliveryStatus.Failed, now.AddDays(-2), attemptCount: 5);
            AddDelivery(db, apiKey, WebhookDeliveryStatus.Delivered, now.AddHours(-3), attemptCount: 1);
            AddDelivery(db, apiKey, WebhookDeliveryStatus.Failed, now.AddHours(-2), attemptCount: 5);
            AddDelivery(db, apiKey, WebhookDeliveryStatus.Failed, now.AddHours(-1), attemptCount: 5);
            AddDelivery(db, apiKey, WebhookDeliveryStatus.Pending, now.AddMinutes(-30), attemptCount: 1);
            AddDelivery(db, apiKey, WebhookDeliveryStatus.InProgress, now.AddMinutes(-20), attemptCount: 2);
            AddDelivery(db, otherApiKey, WebhookDeliveryStatus.Failed, now.AddMinutes(-10), attemptCount: 5);
            await db.SaveChangesAsync();
        }

        await using var metricsDb = fixture.CreateContext();
        var metrics = await new WebhookDeliveryRepository(metricsDb).GetFailureMetricsAsync(
            apiKeyId,
            now,
            CancellationToken.None);

        metrics.ConsecutiveFailures.Should().Be(2);
        metrics.BacklogCount.Should().Be(2);
        metrics.FailedLast24Hours.Should().Be(2);
        metrics.CompletedLast24Hours.Should().Be(3);
        metrics.FailureRate.Should().BeApproximately(2d / 3d, 0.0001);
    }

    private static ApiKey AddApiKey(
        ReplyInMyVoice.Infrastructure.Data.AppDbContext db,
        Guid userId,
        string label)
    {
        var apiKey = new ApiKey
        {
            UserId = userId,
            Name = $"{label} key",
            KeyHash = Guid.NewGuid().ToString("N"),
            Last4 = label[^Math.Min(4, label.Length)..],
            WebhookUrl = "https://93.184.216.34/rewrite",
            WebhookSecret = new string('a', 64),
            CreatedAt = DateTimeOffset.Parse("2026-06-18T09:00:00Z"),
            UpdatedAt = DateTimeOffset.Parse("2026-06-18T09:00:00Z"),
        };
        db.ApiKeys.Add(apiKey);
        return apiKey;
    }

    private static void AddDelivery(
        ReplyInMyVoice.Infrastructure.Data.AppDbContext db,
        ApiKey apiKey,
        WebhookDeliveryStatus status,
        DateTimeOffset activityAt,
        int attemptCount)
    {
        var attempt = new RewriteAttempt
        {
            UserId = apiKey.UserId,
            ApiKey = apiKey,
            IdempotencyKey = Guid.NewGuid().ToString("N"),
            RequestHash = Guid.NewGuid().ToString("N"),
            RequestJson = "{\"roughDraftReply\":\"Thanks for the update.\",\"tone\":\"warm\"}",
            Status = RewriteAttemptStatus.Succeeded,
            ResultJson = "{\"rewrittenText\":\"Thanks for the update.\",\"naturalness\":{\"draftAiLikePercent\":60,\"rewriteAiLikePercent\":20}}",
            CreatedAt = activityAt.AddMinutes(-2),
            CompletedAt = activityAt.AddMinutes(-1),
            ExpiresAt = activityAt.AddMinutes(20),
        };

        db.RewriteAttempts.Add(attempt);
        db.WebhookDeliveries.Add(new WebhookDelivery
        {
            ApiKey = apiKey,
            RewriteAttempt = attempt,
            Url = apiKey.WebhookUrl!,
            Status = status,
            AttemptCount = attemptCount,
            MaxAttempts = 5,
            CreatedAt = activityAt.AddMinutes(-5),
            NextAttemptAt = status == WebhookDeliveryStatus.Pending ? activityAt.AddMinutes(15) : activityAt,
            LastAttemptAt = status is WebhookDeliveryStatus.Pending ? null : activityAt,
            DeliveredAt = status == WebhookDeliveryStatus.Delivered ? activityAt : null,
            LastError = status == WebhookDeliveryStatus.Failed ? "HTTP 500" : null,
            LockedBy = status == WebhookDeliveryStatus.InProgress ? "worker" : null,
            LockedUntil = status == WebhookDeliveryStatus.InProgress ? activityAt.AddMinutes(1) : null,
        });
    }
}
