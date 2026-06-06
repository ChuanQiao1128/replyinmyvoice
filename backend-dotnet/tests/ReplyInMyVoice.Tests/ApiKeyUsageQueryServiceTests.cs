using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using System.Data.Common;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Infrastructure.Data;
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

    [Fact]
    public async Task Summary_and_series_queries_bound_usage_rows_by_window_start_and_clamp_days()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var commands = new CommandCaptureInterceptor();

        AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .AddInterceptors(commands)
                .Options;
            return new AppDbContext(options);
        }

        var now = DateTimeOffset.Parse("2026-06-05T12:00:00+12:00");
        var user = new AppUser
        {
            ExternalAuthUserId = "clerk_usage_window",
            Email = "usage-window@example.com",
            SubscriptionStatus = SubscriptionStatus.Inactive,
            CreatedAt = now,
            UpdatedAt = now,
        };

        await using (var db = CreateContext())
        {
            await db.Database.EnsureCreatedAsync();
            db.AppUsers.Add(user);
            var key = AddKey(db, user.Id, "window key", "3333", now);
            db.ApiKeyUsages.AddRange(
                Usage(key.Id, "req-window-today", "/api/v1/rewrite", 200, 100, now),
                Usage(key.Id, "req-window-old", "/api/v1/rewrite", 200, 100, now.AddDays(-100)));
            await db.SaveChangesAsync();
        }

        var accountService = new AccountService(
            CreateContext,
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["FREE_BASELINE_REWRITES"] = "3",
                })
                .Build());
        var service = new ApiKeyUsageQueryService(CreateContext, accountService);

        commands.Clear();
        var summary = await service.GetSummaryAsync(
            user.ExternalAuthUserId,
            user.Email,
            now,
            CancellationToken.None);
        var series = await service.GetSeriesAsync(user.Id, now, 999, CancellationToken.None);

        summary.Last30dCalls.Should().Be(1);
        series.Should().HaveCount(90);
        series.Sum(x => x.Calls).Should().Be(1);
        commands.CommandTexts.Should().Contain(commandText => HasApiUsageCreatedAtLowerBound(commandText));
    }

    [Fact]
    public async Task Recent_query_ignores_rows_outside_default_window()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var now = DateTimeOffset.UtcNow;

        await using (var db = fixture.CreateContext())
        {
            var key = AddKey(db, user.Id, "recent key", "4444", now);
            db.ApiKeyUsages.AddRange(
                Usage(key.Id, "req-recent-current", "/api/v1/rewrite", 200, 100, now.AddMinutes(-1)),
                Usage(key.Id, "req-recent-old", "/api/v1/rewrite", 200, 100, now.AddDays(-100)));
            await db.SaveChangesAsync();
        }

        var accountService = new AccountService(fixture.CreateContext);
        var service = new ApiKeyUsageQueryService(fixture.CreateContext, accountService);

        var recent = await service.GetRecentAsync(user.Id, 10, CancellationToken.None);

        var item = recent.Should().ContainSingle().Subject;
        item.Endpoint.Should().Be("/api/v1/rewrite");
        item.CreatedAt.Should().BeAfter(now.AddDays(-2));
    }

    private static ApiKey AddKey(
        AppDbContext db,
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

    private static bool HasApiUsageCreatedAtLowerBound(string commandText) =>
        commandText.Contains("ApiKeyUsages", StringComparison.Ordinal) &&
        commandText.Contains("CreatedAt", StringComparison.Ordinal) &&
        commandText.Contains(">=", StringComparison.Ordinal);

    private sealed class CommandCaptureInterceptor : DbCommandInterceptor
    {
        private readonly List<string> _commandTexts = new();

        public IReadOnlyList<string> CommandTexts => _commandTexts;

        public void Clear() => _commandTexts.Clear();

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result)
        {
            _commandTexts.Add(command.CommandText);
            return base.ReaderExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            _commandTexts.Add(command.CommandText);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
