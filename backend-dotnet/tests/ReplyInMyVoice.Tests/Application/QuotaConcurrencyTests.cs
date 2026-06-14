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

namespace ReplyInMyVoice.Tests.Application;

public sealed class QuotaConcurrencyTests
{
    [Fact]
    public async Task Parallel_reserves_with_one_period_slot_remaining_grant_exactly_one()
    {
        const int requestCount = 8;
        var now = DateTimeOffset.Parse("2026-06-13T12:00:00Z");
        await using var fixture = await QuotaFileDbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        await using (var seedDb = fixture.CreateContext())
        {
            seedDb.UsagePeriods.Add(new UsagePeriod
            {
                UserId = user.Id,
                PeriodKey = "free:lifetime",
                QuotaLimit = 3,
                UsedCount = 2,
                ReservedCount = 0,
                CreatedAt = now,
                UpdatedAt = now,
            });
            await seedDb.SaveChangesAsync();
        }

        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks = Enumerable.Range(0, requestCount)
            .Select(index => Task.Run(async () =>
            {
                await start.Task;
                await using var db = fixture.CreateContext();
                return await CreateReserveHandler(db).HandleAsync(ReserveCommand(
                    user.Id,
                    $"period-race-{index}",
                    $"hash-period-race-{index}",
                    now,
                    quotaLimit: 3));
            }))
            .ToArray();

        start.SetResult();
        var results = await Task.WhenAll(tasks);

        results.Count(x => x.Kind == ReserveQuotaResultKind.Created).Should().Be(1);
        results.Count(x => x.Kind == ReserveQuotaResultKind.QuotaExceeded).Should().Be(requestCount - 1);

        await using var verifyDb = fixture.CreateContext();
        var period = await verifyDb.UsagePeriods.SingleAsync();
        period.ReservedCount.Should().Be(1);
        period.UsedCount.Should().Be(2);
        (await verifyDb.RewriteAttempts.CountAsync()).Should().Be(1);
        (await verifyDb.UsageReservations.CountAsync()).Should().Be(1);
        (await verifyDb.OutboxMessages.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Parallel_reserves_with_one_credit_remaining_consume_exactly_one()
    {
        const int requestCount = 8;
        var now = DateTimeOffset.Parse("2026-06-13T12:00:00Z");
        await using var fixture = await QuotaFileDbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        await using (var seedDb = fixture.CreateContext())
        {
            seedDb.UsagePeriods.Add(new UsagePeriod
            {
                UserId = user.Id,
                PeriodKey = "free:lifetime",
                QuotaLimit = 3,
                UsedCount = 3,
                ReservedCount = 0,
                CreatedAt = now,
                UpdatedAt = now,
            });
            seedDb.RewriteCredits.Add(new RewriteCredit
            {
                UserId = user.Id,
                Source = "PROMO",
                AmountGranted = 1,
                AmountConsumed = 0,
                GrantedAt = now.AddDays(-1),
                ExpiresAt = now.AddDays(1),
            });
            await seedDb.SaveChangesAsync();
        }

        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks = Enumerable.Range(0, requestCount)
            .Select(index => Task.Run(async () =>
            {
                await start.Task;
                await using var db = fixture.CreateContext();
                return await CreateReserveHandler(db).HandleAsync(ReserveCommand(
                    user.Id,
                    $"credit-race-{index}",
                    $"hash-credit-race-{index}",
                    now,
                    quotaLimit: 3));
            }))
            .ToArray();

        start.SetResult();
        var results = await Task.WhenAll(tasks);

        results.Count(x => x.Kind == ReserveQuotaResultKind.Created).Should().Be(1);
        results.Count(x => x.Kind == ReserveQuotaResultKind.QuotaExceeded).Should().Be(requestCount - 1);

        await using var verifyDb = fixture.CreateContext();
        var period = await verifyDb.UsagePeriods.SingleAsync();
        period.ReservedCount.Should().Be(0);
        (await verifyDb.RewriteCredits.SingleAsync()).AmountConsumed.Should().Be(1);
        (await verifyDb.UsageReservations.SingleAsync()).RewriteCreditId.Should().NotBeNull();
    }

    [Fact]
    public async Task Finalize_and_expired_cleanup_race_charges_at_most_once()
    {
        var now = DateTimeOffset.Parse("2026-06-13T12:00:00Z");
        var raceNow = now.AddMinutes(2);
        await using var fixture = await QuotaFileDbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        Guid attemptId;
        await using (var reserveDb = fixture.CreateContext())
        {
            var reserve = await CreateReserveHandler(reserveDb).HandleAsync(ReserveCommand(
                user.Id,
                "finalize-expire-race",
                "hash-finalize-expire-race",
                now,
                quotaLimit: 1,
                reservationTtl: TimeSpan.FromMinutes(1)));
            reserve.Kind.Should().Be(ReserveQuotaResultKind.Created);
            attemptId = reserve.AttemptId;
        }

        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finalizeTask = Task.Run(async () =>
        {
            await start.Task;
            await using var db = fixture.CreateContext();
            await CreateFinalizeHandler(db).HandleAsync(new FinalizeQuotaSuccessCommand(
                attemptId,
                "{\"rewrittenText\":\"hello\"}",
                raceNow));
        });
        var expireTask = Task.Run(async () =>
        {
            await start.Task;
            await using var db = fixture.CreateContext();
            return await CreateExpiredHandler(db).HandleAsync(new ReleaseExpiredReservationsCommand(raceNow));
        });

        start.SetResult();
        await Task.WhenAll(finalizeTask, expireTask);

        await using var verifyDb = fixture.CreateContext();
        var period = await verifyDb.UsagePeriods.SingleAsync();
        var attempt = await verifyDb.RewriteAttempts.SingleAsync();
        var reservation = await verifyDb.UsageReservations.SingleAsync();
        var wasFinalized = reservation.Status == UsageReservationStatus.Finalized;

        period.ReservedCount.Should().Be(0);
        (period.UsedCount is 0 or 1).Should().BeTrue();
        period.UsedCount.Should().Be(wasFinalized ? 1 : 0);
        (attempt.Status == RewriteAttemptStatus.Succeeded).Should().Be(wasFinalized);
        wasFinalized.Should().Be(attempt.Status == RewriteAttemptStatus.Succeeded);
        if (!wasFinalized)
        {
            reservation.Status.Should().Be(UsageReservationStatus.Expired);
            attempt.Status.Should().Be(RewriteAttemptStatus.Expired);
        }
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

    private static FinalizeQuotaSuccessHandler CreateFinalizeHandler(AppDbContext db) =>
        new(
            new RewriteAttemptRepository(db),
            new UsageReservationRepository(db),
            new UsagePeriodRepository(db),
            new UnitOfWork(db),
            NullLogger<FinalizeQuotaSuccessHandler>.Instance);

    private static ReleaseExpiredReservationsHandler CreateExpiredHandler(AppDbContext db) =>
        new(
            new UsageReservationRepository(db),
            new UnitOfWork(db),
            NullLogger<ReleaseExpiredReservationsHandler>.Instance);

    private static ReserveQuotaCommand ReserveCommand(
        Guid userId,
        string idempotencyKey,
        string requestHash,
        DateTimeOffset now,
        int quotaLimit,
        TimeSpan? reservationTtl = null) =>
        new(
            userId,
            idempotencyKey,
            requestHash,
            "{\"roughDraftReply\":\"Thanks for your message. I will reply soon.\",\"tone\":\"warm\"}",
            "free:lifetime",
            quotaLimit,
            now,
            reservationTtl ?? TimeSpan.FromMinutes(10),
            ApiKeyId: null);

    private sealed class QuotaFileDbFixture : IAsyncDisposable
    {
        private readonly string _databasePath;

        private QuotaFileDbFixture(string databasePath)
        {
            _databasePath = databasePath;
        }

        public static async Task<QuotaFileDbFixture> CreateAsync()
        {
            var databasePath = Path.Combine(Path.GetTempPath(), $"replyinmyvoice-quota-concurrency-{Guid.NewGuid():N}.db");
            var fixture = new QuotaFileDbFixture(databasePath);
            await using var db = fixture.CreateContext();
            await db.Database.EnsureCreatedAsync();
            await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
            await db.Database.ExecuteSqlRawAsync("PRAGMA busy_timeout=30000;");
            return fixture;
        }

        public AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={_databasePath};Default Timeout=30")
                .EnableSensitiveDataLogging()
                .Options;
            return new AppDbContext(options);
        }

        public async Task<AppUser> CreateUserAsync()
        {
            await using var db = CreateContext();
            var now = DateTimeOffset.Parse("2026-06-13T12:00:00Z");
            var user = new AppUser
            {
                ExternalAuthUserId = $"clerk_quota_concurrency_{Guid.NewGuid():N}",
                Email = "quota-concurrency@example.com",
                SubscriptionStatus = SubscriptionStatus.Inactive,
                CreatedAt = now,
                UpdatedAt = now,
            };
            db.AppUsers.Add(user);
            await db.SaveChangesAsync();
            return user;
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
