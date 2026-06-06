using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Infrastructure.Services;

namespace ReplyInMyVoice.Tests;

public sealed class WebhookDispatcherServiceTests
{
    [Fact]
    public async Task DispatchDueAsync_posts_signed_success_payload_and_marks_delivered()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var signingValue = new string('b', 64);
        var attemptId = await SeedDeliveryAsync(
            fixture,
            user.Id,
            signingValue,
            RewriteAttemptStatus.Succeeded,
            "{\"rewrittenText\":\"Hi Sam, the report is ready.\",\"naturalness\":{\"draftAiLikePercent\":78,\"rewriteAiLikePercent\":24},\"changeSummary\":[],\"riskNotes\":[]}");
        var sender = new RecordingWebhookDeliverySender(new WebhookSendResult(200));
        var dispatcher = new WebhookDispatcherService(fixture.CreateContext, sender);

        var dispatched = await dispatcher.DispatchDueAsync(
            DateTimeOffset.Parse("2026-06-06T02:00:00Z"),
            "test-worker",
            batchSize: 10,
            CancellationToken.None);

        dispatched.Should().Be(1);
        var sent = sender.Requests.Should().ContainSingle().Subject;
        sent.Url.Should().Be("https://listener.example.test/rewrite");

        using var body = JsonDocument.Parse(sent.RawBody);
        body.RootElement.GetProperty("id").GetGuid().Should().Be(attemptId);
        body.RootElement.GetProperty("status").GetString().Should().Be("succeeded");
        body.RootElement.GetProperty("rewrittenText").GetString().Should().Be("Hi Sam, the report is ready.");
        var signal = body.RootElement.GetProperty("signal");
        signal.GetProperty("draft").GetDecimal().Should().Be(78);
        signal.GetProperty("rewrite").GetDecimal().Should().Be(24);

        sent.Signature.Should().Be(ComputeSignature(signingValue, sent.RawBody));
        await using var db = fixture.CreateContext();
        var delivery = await db.WebhookDeliveries.SingleAsync();
        delivery.Status.Should().Be(WebhookDeliveryStatus.Delivered);
        delivery.AttemptCount.Should().Be(1);
        delivery.DeliveredAt.Should().NotBeNull();
        delivery.LastError.Should().BeNull();
    }

    [Fact]
    public async Task DispatchDueAsync_marks_failed_after_retry_limit()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        await SeedDeliveryAsync(
            fixture,
            user.Id,
            new string('c', 64),
            RewriteAttemptStatus.Failed,
            resultJson: null,
            errorCode: "provider_failed",
            attemptCount: 4);
        var sender = new RecordingWebhookDeliverySender(new WebhookSendResult(500));
        var dispatcher = new WebhookDispatcherService(fixture.CreateContext, sender);

        await dispatcher.DispatchDueAsync(
            DateTimeOffset.Parse("2026-06-06T02:00:00Z"),
            "test-worker",
            batchSize: 10,
            CancellationToken.None);

        await using var db = fixture.CreateContext();
        var delivery = await db.WebhookDeliveries.SingleAsync();
        delivery.Status.Should().Be(WebhookDeliveryStatus.Failed);
        delivery.AttemptCount.Should().Be(5);
        delivery.LastError.Should().Contain("HTTP 500");
        delivery.DeliveredAt.Should().BeNull();
    }

    private static async Task<Guid> SeedDeliveryAsync(
        DbFixture fixture,
        Guid userId,
        string signingValue,
        RewriteAttemptStatus status,
        string? resultJson,
        string? errorCode = null,
        int attemptCount = 0)
    {
        await using var db = fixture.CreateContext();
        var apiKey = new ApiKey
        {
            UserId = userId,
            Name = "Server key",
            KeyHash = Guid.NewGuid().ToString("N"),
            Last4 = "abcd",
            WebhookUrl = "https://listener.example.test/rewrite",
            WebhookSecret = signingValue,
            CreatedAt = DateTimeOffset.Parse("2026-06-06T01:59:00Z"),
            UpdatedAt = DateTimeOffset.Parse("2026-06-06T01:59:00Z"),
        };
        var attempt = new RewriteAttempt
        {
            UserId = userId,
            IdempotencyKey = Guid.NewGuid().ToString("N"),
            RequestHash = Guid.NewGuid().ToString("N"),
            RequestJson = "{\"roughDraftReply\":\"Thanks for your message. I will reply soon.\",\"tone\":\"warm\"}",
            Status = status,
            ResultJson = resultJson,
            ErrorCode = errorCode,
            CreatedAt = DateTimeOffset.Parse("2026-06-06T01:58:00Z"),
            CompletedAt = DateTimeOffset.Parse("2026-06-06T01:59:00Z"),
            ExpiresAt = DateTimeOffset.Parse("2026-06-06T02:08:00Z"),
        };
        var delivery = new WebhookDelivery
        {
            ApiKey = apiKey,
            RewriteAttempt = attempt,
            Url = apiKey.WebhookUrl,
            Status = WebhookDeliveryStatus.Pending,
            AttemptCount = attemptCount,
            CreatedAt = DateTimeOffset.Parse("2026-06-06T01:59:00Z"),
            NextAttemptAt = DateTimeOffset.Parse("2026-06-06T01:59:00Z"),
        };
        db.ApiKeys.Add(apiKey);
        db.RewriteAttempts.Add(attempt);
        db.WebhookDeliveries.Add(delivery);
        await db.SaveChangesAsync();
        return attempt.Id;
    }

    private static string ComputeSignature(string signingValue, string rawBody)
    {
        var hash = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(signingValue),
            Encoding.UTF8.GetBytes(rawBody));
        return $"sha256={Convert.ToHexString(hash).ToLowerInvariant()}";
    }
}

internal sealed class RecordingWebhookDeliverySender(params WebhookSendResult[] results) : IWebhookDeliverySender
{
    private readonly Queue<WebhookSendResult> _results = new(results);
    private readonly List<WebhookSendRequest> _requests = [];

    public IReadOnlyList<WebhookSendRequest> Requests => _requests;

    public Task<WebhookSendResult> SendAsync(
        WebhookSendRequest request,
        CancellationToken cancellationToken)
    {
        _requests.Add(request);
        return Task.FromResult(_results.Count == 0 ? new WebhookSendResult(200) : _results.Dequeue());
    }
}
