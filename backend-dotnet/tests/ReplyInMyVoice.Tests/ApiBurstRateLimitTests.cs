using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ReplyInMyVoice.Application.Common;
using ReplyInMyVoice.Application.UseCases.Quota;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Infrastructure.Data;
using ReplyInMyVoice.Infrastructure.Repositories;
using ReplyInMyVoice.Infrastructure.Services;

namespace ReplyInMyVoice.Tests;

public sealed class ApiBurstRateLimitTests
{
    [Fact]
    public async Task Concurrent_burst_allows_exactly_limit_and_rejects_without_quota_reservation()
    {
        const int rateLimitPerMinute = 5;
        const int requestCount = 20;
        const string periodKey = "api:2026-06";
        var now = DateTimeOffset.Parse("2026-06-08T12:34:10Z");
        await using var fixture = await FileBackedApiBurstDbFixture.CreateAsync();
        var (user, apiKey) = await SeedUserAndApiKeyAsync(fixture, rateLimitPerMinute);
        var limiter = new ApiKeyRateLimiter(fixture.CreateContext);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var tasks = Enumerable.Range(0, requestCount)
            .Select(index => Task.Run(async () =>
            {
                    await start.Task;
                    return await SubmitAfterRateLimitAsync(
                        limiter,
                        fixture.CreateContext,
                        user.Id,
                        apiKey.Id,
                    rateLimitPerMinute,
                    requestCount + 10,
                    periodKey,
                    $"burst-{index}",
                    $"hash-burst-{index}",
                    now);
            }))
            .ToArray();

        start.SetResult();
        var results = await Task.WhenAll(tasks);

        results.Count(x => x.RateLimit.IsUnavailable).Should().Be(0);
        results.Count(x => x.IsAdmitted).Should().Be(rateLimitPerMinute);
        results.Count(x => x.IsRateLimited).Should().Be(requestCount - rateLimitPerMinute);
        results.Where(x => x.IsAdmitted)
            .Select(x => x.Reservation!.Kind)
            .Should()
            .OnlyContain(kind => kind == ReserveQuotaResultKind.Created);
        results.Where(x => x.IsRateLimited).All(result => result.Reservation is null).Should().BeTrue();

        await using var db = fixture.CreateContext();
        var window = await db.ApiKeyRateLimitWindows.SingleAsync(x => x.ApiKeyId == apiKey.Id);
        window.WindowStart.Should().Be(DateTimeOffset.Parse("2026-06-08T12:34:00Z"));
        window.Count.Should().Be(rateLimitPerMinute);
        (await db.RewriteAttempts.CountAsync()).Should().Be(rateLimitPerMinute);
        (await db.UsageReservations.CountAsync()).Should().Be(rateLimitPerMinute);
        (await db.OutboxMessages.CountAsync()).Should().Be(rateLimitPerMinute);
        var period = await db.UsagePeriods.SingleAsync(x => x.UserId == user.Id && x.PeriodKey == periodKey);
        period.ReservedCount.Should().Be(rateLimitPerMinute);
        period.UsedCount.Should().Be(0);
    }

    [Fact]
    public async Task Rate_limit_window_resets_in_next_minute()
    {
        const int rateLimitPerMinute = 3;
        const string periodKey = "api:2026-06-reset";
        var firstWindow = DateTimeOffset.Parse("2026-06-08T12:34:10Z");
        var nextWindow = DateTimeOffset.Parse("2026-06-08T12:35:01Z");
        await using var fixture = await FileBackedApiBurstDbFixture.CreateAsync();
        var (user, apiKey) = await SeedUserAndApiKeyAsync(fixture, rateLimitPerMinute);
        var limiter = new ApiKeyRateLimiter(fixture.CreateContext);

        var firstWindowResults = await Task.WhenAll(
            Enumerable.Range(0, rateLimitPerMinute)
                .Select(index => SubmitAfterRateLimitAsync(
                    limiter,
                    fixture.CreateContext,
                    user.Id,
                    apiKey.Id,
                    rateLimitPerMinute,
                    rateLimitPerMinute + 2,
                    periodKey,
                    $"reset-{index}",
                    $"hash-reset-{index}",
                    firstWindow)));
        var overLimit = await SubmitAfterRateLimitAsync(
            limiter,
            fixture.CreateContext,
            user.Id,
            apiKey.Id,
            rateLimitPerMinute,
            rateLimitPerMinute + 2,
            periodKey,
            "reset-over-limit",
            "hash-reset-over-limit",
            firstWindow);
        var afterReset = await SubmitAfterRateLimitAsync(
            limiter,
            fixture.CreateContext,
            user.Id,
            apiKey.Id,
            rateLimitPerMinute,
            rateLimitPerMinute + 2,
            periodKey,
            "reset-next-window",
            "hash-reset-next-window",
            nextWindow);

        firstWindowResults.Count(x => x.IsAdmitted).Should().Be(rateLimitPerMinute);
        overLimit.IsRateLimited.Should().BeTrue();
        overLimit.Reservation.Should().BeNull();
        afterReset.IsAdmitted.Should().BeTrue();
        afterReset.Reservation!.Kind.Should().Be(ReserveQuotaResultKind.Created);

        await using var db = fixture.CreateContext();
        var windows = (await db.ApiKeyRateLimitWindows
            .Where(x => x.ApiKeyId == apiKey.Id)
            .ToListAsync())
            .OrderBy(x => x.WindowStart)
            .ToList();
        windows.Select(x => x.Count).Should().Equal(rateLimitPerMinute, 1);
        windows.Select(x => x.WindowStart).Should().Equal(
            DateTimeOffset.Parse("2026-06-08T12:34:00Z"),
            DateTimeOffset.Parse("2026-06-08T12:35:00Z"));
        (await db.RewriteAttempts.CountAsync()).Should().Be(rateLimitPerMinute + 1);
        (await db.UsageReservations.CountAsync()).Should().Be(rateLimitPerMinute + 1);
        var period = await db.UsagePeriods.SingleAsync(x => x.UserId == user.Id && x.PeriodKey == periodKey);
        period.ReservedCount.Should().Be(rateLimitPerMinute + 1);
        period.UsedCount.Should().Be(0);
    }

    private static async Task<SimulatedSubmitResult> SubmitAfterRateLimitAsync(
        IApiKeyRateLimiter limiter,
        Func<AppDbContext> createContext,
        Guid userId,
        Guid apiKeyId,
        int rateLimitPerMinute,
        int quotaLimit,
        string periodKey,
        string idempotencyKey,
        string requestHash,
        DateTimeOffset now)
    {
        var rateLimit = await limiter.CheckAndIncrementAsync(
            apiKeyId,
            rateLimitPerMinute,
            now,
            CancellationToken.None);

        if (rateLimit.IsLimited || rateLimit.IsUnavailable)
        {
            return new SimulatedSubmitResult(rateLimit, null);
        }

        await using var reserveDb = createContext();
        var reservation = await CreateReserveHandler(reserveDb).HandleAsync(
            new ReserveQuotaCommand(
                userId,
                idempotencyKey,
                requestHash,
                RequestJson(idempotencyKey),
                periodKey,
                quotaLimit,
                now,
                TimeSpan.FromMinutes(15),
                apiKeyId),
            CancellationToken.None);

        return new SimulatedSubmitResult(rateLimit, reservation);
    }

    private static ReserveQuotaHandler CreateReserveHandler(AppDbContext db) =>
        new(
            new UsagePeriodRepository(db),
            new RewriteAttemptRepository(db),
            new UsageReservationRepository(db),
            new RewriteCreditRepository(db),
            new OutboxMessageRepository(db),
            new UnitOfWork(db),
            NullLogger<ReserveQuotaHandler>.Instance);

    private static async Task<(AppUser User, ApiKey ApiKey)> SeedUserAndApiKeyAsync(
        FileBackedApiBurstDbFixture fixture,
        int rateLimitPerMinute)
    {
        var now = DateTimeOffset.Parse("2026-06-08T12:00:00Z");
        await using var db = fixture.CreateContext();
        var user = new AppUser
        {
            ExternalAuthUserId = $"clerk_api_burst_{Guid.NewGuid():N}",
            Email = "api-burst@example.com",
            StripeCustomerId = "cus_api_burst",
            StripeSubscriptionId = "sub_api_burst",
            SubscriptionStatus = SubscriptionStatus.Active,
            CurrentPeriodEnd = now.AddMonths(1),
            CreatedAt = now,
            UpdatedAt = now,
        };
        var apiKey = new ApiKey
        {
            User = user,
            Name = "API burst test key",
            KeyHash = $"api-burst-key-{Guid.NewGuid():N}",
            Last4 = "test",
            RateLimitPerMinute = rateLimitPerMinute,
            CreatedAt = now,
            UpdatedAt = now,
        };

        db.AppUsers.Add(user);
        db.ApiKeys.Add(apiKey);
        await db.SaveChangesAsync();
        return (user, apiKey);
    }

    private static string RequestJson(string idempotencyKey) =>
        $$"""{"draft":"{{idempotencyKey}} asks for a concise update after reviewing the client report.","tone":"warm"}""";

    private sealed record SimulatedSubmitResult(
        ApiKeyRateLimitResult RateLimit,
        ReserveQuotaResult? Reservation)
    {
        public bool IsAdmitted => !RateLimit.IsLimited && !RateLimit.IsUnavailable;
        public bool IsRateLimited => RateLimit.IsLimited;
    }

    private sealed class FileBackedApiBurstDbFixture : IAsyncDisposable
    {
        private readonly string _databasePath;

        private FileBackedApiBurstDbFixture(string databasePath)
        {
            _databasePath = databasePath;
        }

        public static async Task<FileBackedApiBurstDbFixture> CreateAsync()
        {
            var databasePath = Path.Combine(Path.GetTempPath(), $"replyinmyvoice-api-burst-{Guid.NewGuid():N}.db");
            var fixture = new FileBackedApiBurstDbFixture(databasePath);
            await using var db = fixture.CreateContext();
            await db.Database.EnsureCreatedAsync();
            await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
            await db.Database.ExecuteSqlRawAsync("PRAGMA busy_timeout=5000;");
            return fixture;
        }

        public AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={_databasePath};Default Timeout=5")
                .EnableSensitiveDataLogging()
                .Options;
            return new AppDbContext(options);
        }

        public ValueTask DisposeAsync()
        {
            SqliteConnection.ClearAllPools();
            TryDelete(_databasePath);
            TryDelete($"{_databasePath}-wal");
            TryDelete($"{_databasePath}-shm");
            return ValueTask.CompletedTask;
        }

        private static void TryDelete(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
