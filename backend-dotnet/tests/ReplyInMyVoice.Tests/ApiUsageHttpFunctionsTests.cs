using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Functions.Functions;
using ReplyInMyVoice.Infrastructure.Data;
using ReplyInMyVoice.Infrastructure.Services;

namespace ReplyInMyVoice.Tests;

public sealed class ApiUsageHttpFunctionsTests
{
    [Fact]
    public async Task Usage_endpoints_require_authentication()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var functions = CreateFunctions(fixture.CreateContext);
        var request = CreateRequest();

        var summary = await functions.GetApiUsageSummary(request, CancellationToken.None);
        var series = await functions.GetApiUsageSeries(request, CancellationToken.None);
        var recent = await functions.GetApiUsageRecent(request, CancellationToken.None);

        summary.Should().BeAssignableTo<ObjectResult>()
            .Which.StatusCode.Should().Be((int)HttpStatusCode.Unauthorized);
        series.Should().BeAssignableTo<ObjectResult>()
            .Which.StatusCode.Should().Be((int)HttpStatusCode.Unauthorized);
        recent.Should().BeAssignableTo<ObjectResult>()
            .Which.StatusCode.Should().Be((int)HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Usage_endpoints_return_authenticated_user_usage()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var now = DateTimeOffset.UtcNow;
        await using (var db = fixture.CreateContext())
        {
            var owner = new AppUser
            {
                ExternalAuthUserId = "entra-usage-owner",
                Email = "usage-owner@example.com",
                SubscriptionStatus = SubscriptionStatus.Inactive,
                CreatedAt = now,
                UpdatedAt = now,
            };
            var other = new AppUser
            {
                ExternalAuthUserId = "entra-usage-other",
                Email = "usage-other@example.com",
                SubscriptionStatus = SubscriptionStatus.Inactive,
                CreatedAt = now,
                UpdatedAt = now,
            };
            db.AppUsers.AddRange(owner, other);
            var ownerKey = AddKey(db, owner.Id, "1111", now);
            var otherKey = AddKey(db, other.Id, "9999", now);
            db.ApiKeyUsages.AddRange(
                Usage(ownerKey.Id, "req-owner-today", "/api/v1/rewrite", 200, now),
                Usage(ownerKey.Id, "req-owner-yesterday", "/api/v1/usage", 500, now.AddDays(-1)),
                Usage(otherKey.Id, "req-other-today", "/api/v1/rewrite-other", 200, now));
            await db.SaveChangesAsync();
        }

        var functions = CreateFunctions(fixture.CreateContext);

        var summaryResult = await functions.GetApiUsageSummary(
            CreateRequest("entra-usage-owner", "usage-owner@example.com"),
            CancellationToken.None);
        var seriesResult = await functions.GetApiUsageSeries(
            CreateRequest("entra-usage-owner", "usage-owner@example.com", "?days=2"),
            CancellationToken.None);
        var recentResult = await functions.GetApiUsageRecent(
            CreateRequest("entra-usage-owner", "usage-owner@example.com", "?limit=1"),
            CancellationToken.None);

        var summary = summaryResult.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<ApiUsageSummaryResponse>().Subject;
        summary.Today.Calls.Should().Be(1);
        summary.Today.Succeeded.Should().Be(1);
        summary.Last30dCalls.Should().Be(2);

        var series = seriesResult.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeAssignableTo<IReadOnlyList<ApiUsageSeriesPoint>>().Subject;
        series.Should().HaveCount(2);
        series.Sum(x => x.Calls).Should().Be(2);

        var recent = recentResult.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeAssignableTo<IReadOnlyList<ApiUsageRecentItem>>().Subject;
        var item = recent.Should().ContainSingle().Subject;
        item.Endpoint.Should().Be("/api/v1/rewrite");
        item.KeyLast4.Should().Be("1111");
    }

    private static ApiUsageHttpFunctions CreateFunctions(Func<AppDbContext> createContext)
    {
        var accountService = new AccountService(createContext);
        return new ApiUsageHttpFunctions(
            BuildConfiguration(),
            accountService,
            new ApiKeyUsageQueryService(createContext, accountService));
    }

    private static HttpRequest CreateRequest(
        string? externalAuthUserId = null,
        string? email = null,
        string? queryString = null)
    {
        var context = new DefaultHttpContext();
        if (!string.IsNullOrWhiteSpace(externalAuthUserId))
        {
            context.Request.Headers["X-External-User-Id"] = externalAuthUserId;
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            context.Request.Headers["X-User-Email"] = email;
        }

        if (!string.IsNullOrWhiteSpace(queryString))
        {
            context.Request.QueryString = new QueryString(queryString);
        }

        return context.Request;
    }

    private static ApiKey AddKey(
        AppDbContext db,
        Guid userId,
        string last4,
        DateTimeOffset now)
    {
        var key = new ApiKey
        {
            UserId = userId,
            Name = $"Key {last4}",
            KeyHash = $"hash-{Guid.NewGuid():N}",
            Last4 = last4,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.ApiKeys.Add(key);
        return key;
    }

    private static ApiKeyUsage Usage(
        Guid apiKeyId,
        string requestId,
        string endpoint,
        int statusCode,
        DateTimeOffset createdAt) =>
        new()
        {
            ApiKeyId = apiKeyId,
            RequestId = requestId,
            Endpoint = endpoint,
            StatusCode = statusCode,
            CreatedAt = createdAt,
        };

    private static IConfiguration BuildConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ALLOW_HEADER_AUTH"] = "true",
                ["AZURE_FUNCTIONS_ENVIRONMENT"] = "Testing",
            })
            .Build();
}
