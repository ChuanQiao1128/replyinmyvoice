using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Functions.Functions;
using ReplyInMyVoice.Infrastructure.Repositories;
using ReplyInMyVoice.Infrastructure.Services;

namespace ReplyInMyVoice.Tests;

[Collection("ApiKeyPepper")]
public sealed class WebhookStatusHttpFunctionTests
{
    private const string TestPepper = "webhook-status-test-pepper";

    [Fact]
    public async Task GetWebhookDeliveryStatus_returns()
    {
        Environment.SetEnvironmentVariable("API_KEY_PEPPER", TestPepper);
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var otherUser = await fixture.CreateUserAsync();
        var token = "rmv_live_webhook_status_key";
        var now = DateTimeOffset.UtcNow;
        Guid apiKeyId;
        Guid ownerDeliveryId;
        Guid otherDeliveryId;

        await using (var db = fixture.CreateContext())
        {
            var apiKey = AddApiKey(db, user.Id, token);
            var otherApiKey = AddApiKey(db, otherUser.Id, "rmv_live_other_webhook_status_key");
            apiKeyId = apiKey.Id;
            ownerDeliveryId = AddDelivery(db, apiKey, WebhookDeliveryStatus.Failed, now.AddMinutes(-10), attemptCount: 5);
            AddDelivery(db, apiKey, WebhookDeliveryStatus.Pending, now.AddMinutes(-5), attemptCount: 1);
            otherDeliveryId = AddDelivery(db, otherApiKey, WebhookDeliveryStatus.Failed, now.AddMinutes(-1), attemptCount: 5);
            await db.SaveChangesAsync();
        }

        await using var functionDb = fixture.CreateContext();
        var function = new WebhookStatusHttpFunction(
            functionDb,
            new WebhookDeliveryRepository(functionDb));

        var result = await function.GetWebhookDeliveryStatus(
            CreateBearerRequest(token),
            CancellationToken.None);

        var response = result.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<WebhookDeliveryStatusResponse>().Subject;
        response.ApiKeyId.Should().Be(apiKeyId);
        response.Metrics.ConsecutiveFailures.Should().Be(1);
        response.Metrics.BacklogCount.Should().Be(1);
        response.Metrics.FailedLast24Hours.Should().Be(1);
        response.Metrics.CompletedLast24Hours.Should().Be(1);
        response.Deliveries.Select(x => x.Id).Should().Contain(ownerDeliveryId);
        response.Deliveries.Select(x => x.Id).Should().NotContain(otherDeliveryId);
    }

    private static ApiKey AddApiKey(
        ReplyInMyVoice.Infrastructure.Data.AppDbContext db,
        Guid userId,
        string token)
    {
        var apiKey = new ApiKey
        {
            UserId = userId,
            Name = "Webhook status key",
            KeyHash = ApiKeyHashing.ComputeHash(token),
            Last4 = token[^4..],
            WebhookUrl = "https://93.184.216.34/rewrite",
            WebhookSecret = new string('b', 64),
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-1),
            UpdatedAt = DateTimeOffset.UtcNow.AddHours(-1),
        };
        db.ApiKeys.Add(apiKey);
        return apiKey;
    }

    private static Guid AddDelivery(
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
        var delivery = new WebhookDelivery
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
        };
        db.RewriteAttempts.Add(attempt);
        db.WebhookDeliveries.Add(delivery);
        return delivery.Id;
    }

    private static HttpRequest CreateBearerRequest(string token)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = $"Bearer {token}";
        return context.Request;
    }
}
