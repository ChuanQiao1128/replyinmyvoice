using FluentAssertions;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.UseCases.WebhookOutbox;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Infrastructure.Repositories;
using ReplyInMyVoice.Tests.TestDoubles;

namespace ReplyInMyVoice.Tests.Application;

public sealed class DispatchDueWebhooksHandlerMetricsTests
{
    [Fact]
    public async Task HandleAsync_emits_failed_total()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var now = DateTimeOffset.Parse("2026-06-18T12:00:00Z");
        var apiKeyId = await SeedWebhookDeliveryAsync(fixture, user.Id, now, attemptCount: 4, nextAttemptAt: now);
        var metrics = new RecordingBusinessMetrics();
        await using var handlerDb = fixture.CreateContext();
        var handler = CreateHandler(handlerDb, new RecordingWebhookDeliverySender(new WebhookSendResult(500)), metrics);

        await handler.HandleAsync(
            new DispatchDueWebhooksCommand(now, "metrics-worker", BatchSize: 10),
            CancellationToken.None);

        metrics.Records.Should().ContainSingle(record =>
            record.Name == BusinessMetricNames.WebhookDeliveryFailedTotal &&
            record.Value == 1 &&
            record.DimensionName == BusinessMetricDimensions.ApiKeyId &&
            record.DimensionValue == apiKeyId.ToString("D"));
    }

    [Fact]
    public async Task HandleAsync_emits_backlog()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var now = DateTimeOffset.Parse("2026-06-18T12:00:00Z");
        var apiKeyId = await SeedWebhookDeliveryAsync(fixture, user.Id, now, attemptCount: 4, nextAttemptAt: now);
        await SeedWebhookDeliveryAsync(
            fixture,
            user.Id,
            now,
            attemptCount: 1,
            nextAttemptAt: now.AddMinutes(10),
            existingApiKeyId: apiKeyId);
        var metrics = new RecordingBusinessMetrics();
        await using var handlerDb = fixture.CreateContext();
        var handler = CreateHandler(handlerDb, new RecordingWebhookDeliverySender(new WebhookSendResult(500)), metrics);

        await handler.HandleAsync(
            new DispatchDueWebhooksCommand(now, "metrics-worker", BatchSize: 10),
            CancellationToken.None);

        metrics.Records.Should().ContainSingle(record =>
            record.Name == BusinessMetricNames.WebhookDeliveryBacklog &&
            record.Value == 1 &&
            record.DimensionName == BusinessMetricDimensions.ApiKeyId &&
            record.DimensionValue == apiKeyId.ToString("D"));
    }

    private static DispatchDueWebhooksHandler CreateHandler(
        ReplyInMyVoice.Infrastructure.Data.AppDbContext db,
        IWebhookDeliverySender sender,
        IBusinessMetrics metrics) =>
        new(
            new WebhookDeliveryRepository(db),
            sender,
            new UnitOfWork(db),
            metrics);

    private static async Task<Guid> SeedWebhookDeliveryAsync(
        DbFixture fixture,
        Guid userId,
        DateTimeOffset now,
        int attemptCount,
        DateTimeOffset nextAttemptAt,
        Guid? existingApiKeyId = null)
    {
        await using var db = fixture.CreateContext();
        ApiKey apiKey;
        if (existingApiKeyId is { } apiKeyId)
        {
            apiKey = await db.ApiKeys.FindAsync(apiKeyId)
                ?? throw new InvalidOperationException("Test API key was not found.");
        }
        else
        {
            apiKey = new ApiKey
            {
                UserId = userId,
                Name = "Metrics key",
                KeyHash = Guid.NewGuid().ToString("N"),
                Last4 = "rics",
                WebhookUrl = "https://93.184.216.34/rewrite",
                WebhookSecret = new string('d', 64),
                CreatedAt = now.AddHours(-1),
                UpdatedAt = now.AddHours(-1),
            };
            db.ApiKeys.Add(apiKey);
        }

        var attempt = new RewriteAttempt
        {
            UserId = userId,
            ApiKey = apiKey,
            IdempotencyKey = Guid.NewGuid().ToString("N"),
            RequestHash = Guid.NewGuid().ToString("N"),
            RequestJson = "{\"roughDraftReply\":\"Thanks for the update.\",\"tone\":\"warm\"}",
            Status = RewriteAttemptStatus.Succeeded,
            ResultJson = "{\"rewrittenText\":\"Thanks for the update.\",\"naturalness\":{\"draftAiLikePercent\":60,\"rewriteAiLikePercent\":20}}",
            CreatedAt = now.AddMinutes(-10),
            CompletedAt = now.AddMinutes(-9),
            ExpiresAt = now.AddMinutes(20),
        };
        db.RewriteAttempts.Add(attempt);
        db.WebhookDeliveries.Add(new WebhookDelivery
        {
            ApiKey = apiKey,
            RewriteAttempt = attempt,
            Url = apiKey.WebhookUrl!,
            Status = WebhookDeliveryStatus.Pending,
            AttemptCount = attemptCount,
            MaxAttempts = 5,
            CreatedAt = now.AddMinutes(-8),
            NextAttemptAt = nextAttemptAt,
        });
        await db.SaveChangesAsync();
        return apiKey.Id;
    }
}
