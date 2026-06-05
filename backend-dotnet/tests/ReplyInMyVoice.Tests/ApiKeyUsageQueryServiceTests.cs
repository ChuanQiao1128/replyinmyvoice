using FluentAssertions;
using Microsoft.Extensions.Configuration;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Infrastructure.Services;

namespace ReplyInMyVoice.Tests;

public sealed class ApiKeyUsageQueryServiceTests
{
    [Fact]
    public async Task Usage_queries_scope_to_user_keys_and_bucket_by_auckland_day()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var userA = await fixture.CreateUserAsync();
        var userB = await fixture.CreateUserAsync();
        var now = DateTimeOffset.Parse("2026-06-05T12:00:00+12:00");
        var today = "2026-06-05";
        var yesterday = "2026-06-04";

        await using (var db = fixture.CreateContext())
        {
            var keyA1 = AddKey(db, userA.Id, "A primary", "1111", now);
            var keyA2 = AddKey(db, userA.Id, "A secondary", "2222", now);
            var keyB = AddKey(db, userB.Id, "B primary", "9999", now);

            db.ApiKeyUsages.AddRange(
                Usage(keyA1.Id, "req-a-1", "/api/v1/rewrite", 200, 120, DateTimeOffset.Parse("2026-06-04T11:59:00Z")),
                Usage(keyA1.Id, "req-a-2", "/api/v1/rewrite", 500, 400, DateTimeOffset.Parse("2026-06-04T12:01:00Z")),
                Usage(keyA2.Id, "req-a-3", "/api/v1/rewrite", 202, 180, DateTimeOffset.Parse("2026-06-03T12:30:00Z")),
                Usage(keyA1.Id, "req-a-4", "/api/v1/usage", 401, null, DateTimeOffset.Parse("2026-05-20T00:00:00Z")),
                Usage(keyB.Id, "req-b-1", "/api/v1/rewrite-b", 200, 90, DateTimeOffset.Parse("2026-06-04T22:00:00Z")));
            db.UsagePeriods.Add(new UsagePeriod
            {
                UserId = userA.Id,
                PeriodKey = "free:lifetime",
                UsedCount = 1,
                ReservedCount = 0,
                CreatedAt = now,
                UpdatedAt = now,
            });
            await db.SaveChangesAsync();
        }

        var accountService = new AccountService(
            fixture.CreateContext,
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["FREE_BASELINE_REWRITES"] = "3",
                })
                .Build());
        var service = new ApiKeyUsageQueryService(fixture.CreateContext, accountService);

        var summary = await service.GetSummaryAsync(
            userA.ExternalAuthUserId,
            userA.Email,
            now,
            CancellationToken.None);
        var series = await service.GetSeriesAsync(userA.Id, now, 3, CancellationToken.None);
        var recent = await service.GetRecentAsync(userA.Id, 50, CancellationToken.None);

        summary.Today.Should().Be(new ApiUsageCount(1, 0, 1));
        summary.Yesterday.Should().Be(new ApiUsageCount(2, 2, 0));
        summary.MonthToDate.Should().Be(new ApiUsageCount(3, 2, 1));
        summary.Last30dCalls.Should().Be(4);
        summary.Quota.Should().Be(3);
        summary.Used.Should().Be(1);
        summary.Remaining.Should().Be(2);
        summary.PeriodEnd.Should().BeNull();

        series.Should().BeEquivalentTo(
            new[]
            {
                new ApiUsageSeriesPoint("2026-06-03", 0, 0, 0),
                new ApiUsageSeriesPoint(yesterday, 2, 2, 0),
                new ApiUsageSeriesPoint(today, 1, 0, 1),
            },
            options => options.WithStrictOrdering());

        recent.Should().HaveCount(4);
        recent.Select(x => x.KeyLast4).Should().OnlyContain(last4 => last4 == "1111" || last4 == "2222");
        recent.Select(x => x.Endpoint).Should().NotContain("/api/v1/rewrite-b");
        recent.Select(x => x.CreatedAt).Should().BeInDescendingOrder();
    }

    private static ApiKey AddKey(
        ReplyInMyVoice.Infrastructure.Data.AppDbContext db,
        Guid userId,
        string name,
        string last4,
        DateTimeOffset now)
    {
        var key = new ApiKey
        {
            UserId = userId,
            Name = name,
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
        int? latencyMs,
        DateTimeOffset createdAt) =>
        new()
        {
            ApiKeyId = apiKeyId,
            RequestId = requestId,
            Endpoint = endpoint,
            StatusCode = statusCode,
            LatencyMs = latencyMs,
            CreatedAt = createdAt,
        };
}
