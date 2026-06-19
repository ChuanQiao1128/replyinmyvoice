using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Functions.Functions;

namespace ReplyInMyVoice.Tests;

public sealed class AdminRetryWebhookDeliveryTests
{
    [Fact]
    public async Task RetryWebhookDelivery_resets()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var now = DateTimeOffset.Parse("2026-06-18T12:00:00Z");
        Guid deliveryId;

        await using (var db = fixture.CreateContext())
        {
            deliveryId = AddFailedDelivery(db, user.Id, now);
            await db.SaveChangesAsync();
        }

        var function = AdminHttpFunctionsTestFactory.Create(
            BuildConfiguration(),
            fixture.CreateContext);

        var result = await function.RetryWebhookDelivery(
            CreateAdminRequest(),
            deliveryId.ToString("D"),
            CancellationToken.None);

        var response = result.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<AdminWebhookDeliveryRetryResponse>().Subject;
        response.Id.Should().Be(deliveryId);
        response.Status.Should().Be("Pending");
        response.AttemptCount.Should().Be(0);

        await using var verifyDb = fixture.CreateContext();
        var delivery = await verifyDb.WebhookDeliveries.FindAsync(deliveryId);
        delivery.Should().NotBeNull();
        delivery!.Status.Should().Be(WebhookDeliveryStatus.Pending);
        delivery.AttemptCount.Should().Be(0);
        delivery.NextAttemptAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(10));
        delivery.LastError.Should().BeNull();
        delivery.LockedBy.Should().BeNull();
        delivery.LockedUntil.Should().BeNull();
    }

    private static Guid AddFailedDelivery(
        ReplyInMyVoice.Infrastructure.Data.AppDbContext db,
        Guid userId,
        DateTimeOffset now)
    {
        var apiKey = new ApiKey
        {
            UserId = userId,
            Name = "Retry key",
            KeyHash = Guid.NewGuid().ToString("N"),
            Last4 = "etry",
            WebhookUrl = "https://93.184.216.34/rewrite",
            WebhookSecret = new string('c', 64),
            CreatedAt = now.AddHours(-1),
            UpdatedAt = now.AddHours(-1),
        };
        var attempt = new RewriteAttempt
        {
            UserId = userId,
            ApiKey = apiKey,
            IdempotencyKey = Guid.NewGuid().ToString("N"),
            RequestHash = Guid.NewGuid().ToString("N"),
            RequestJson = "{\"roughDraftReply\":\"Thanks for the update.\",\"tone\":\"warm\"}",
            Status = RewriteAttemptStatus.Succeeded,
            ResultJson = "{\"rewrittenText\":\"Thanks for the update.\",\"naturalness\":{\"draftAiLikePercent\":60,\"rewriteAiLikePercent\":20}}",
            CreatedAt = now.AddHours(-1),
            CompletedAt = now.AddMinutes(-50),
            ExpiresAt = now.AddMinutes(10),
        };
        var delivery = new WebhookDelivery
        {
            ApiKey = apiKey,
            RewriteAttempt = attempt,
            Url = apiKey.WebhookUrl,
            Status = WebhookDeliveryStatus.Failed,
            AttemptCount = 5,
            MaxAttempts = 5,
            CreatedAt = now.AddMinutes(-45),
            NextAttemptAt = now.AddMinutes(-40),
            LastAttemptAt = now.AddMinutes(-40),
            LastError = "HTTP 500",
            LockedBy = "worker",
            LockedUntil = now.AddMinutes(5),
        };
        db.ApiKeys.Add(apiKey);
        db.RewriteAttempts.Add(attempt);
        db.WebhookDeliveries.Add(delivery);
        return delivery.Id;
    }

    private static HttpRequest CreateAdminRequest()
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("oid", "admin-owner-oid"),
                new Claim("email", "owner@example.com"),
            ], "Bearer")),
        };

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
