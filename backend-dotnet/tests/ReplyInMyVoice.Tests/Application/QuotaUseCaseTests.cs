using System.Data;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.Common;
using ReplyInMyVoice.Application.UseCases.Quota;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Infrastructure.Data;
using ReplyInMyVoice.Infrastructure.Repositories;

namespace ReplyInMyVoice.Tests.Application;

public sealed class QuotaUseCaseTests
{
    [Fact]
    public async Task ReserveQuotaAsync_creates_pending_attempt_reservation_and_job_outbox()
    {
        await using var fixture = await QuotaUseCaseDbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var now = DateTimeOffset.Parse("2026-06-09T00:00:00Z");
        await using var handlerDb = fixture.CreateContext();
        var handler = CreateReserveHandler(handlerDb);

        var result = await handler.HandleAsync(ReserveCommand(
            user.Id,
            "idem-create",
            "hash-create",
            now,
            quotaLimit: 3,
            reservationTtl: TimeSpan.FromMinutes(10)));

        result.Kind.Should().Be(ReserveQuotaResultKind.Created);
        result.Status.Should().Be(RewriteAttemptStatus.Pending);

        await using var verifyDb = fixture.CreateContext();
        var period = await verifyDb.UsagePeriods.SingleAsync();
        period.UsedCount.Should().Be(0);
        period.ReservedCount.Should().Be(1);

        var attempt = await verifyDb.RewriteAttempts.SingleAsync();
        attempt.Id.Should().Be(result.AttemptId);
        attempt.Status.Should().Be(RewriteAttemptStatus.Pending);
        attempt.ExpiresAt.Should().Be(now.AddMinutes(10));

        var reservation = await verifyDb.UsageReservations.SingleAsync();
        reservation.Status.Should().Be(UsageReservationStatus.Pending);
        reservation.ExpiresAt.Should().Be(now.AddMinutes(10));
        reservation.RewriteCreditId.Should().BeNull();

        var outbox = await verifyDb.OutboxMessages.SingleAsync();
        outbox.Status.Should().Be(OutboxMessageStatus.Pending);
        outbox.MessageType.Should().Be("RewriteJobCreated");
        outbox.CorrelationId.Should().Be(attempt.Id.ToString());
        outbox.PayloadJson.Should().Contain(attempt.Id.ToString());
    }

    [Fact]
    public async Task ReserveQuotaAsync_returns_existing_or_conflict_for_reused_idempotency_key()
    {
        await using var fixture = await QuotaUseCaseDbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var now = DateTimeOffset.Parse("2026-06-09T00:00:00Z");
        await using var handlerDb = fixture.CreateContext();
        var handler = CreateReserveHandler(handlerDb);

        var first = await handler.HandleAsync(ReserveCommand(user.Id, "idem-repeat", "hash-one", now, quotaLimit: 3));
        var existing = await handler.HandleAsync(ReserveCommand(user.Id, "idem-repeat", "hash-one", now.AddSeconds(1), quotaLimit: 3));
        var conflict = await handler.HandleAsync(ReserveCommand(user.Id, "idem-repeat", "hash-two", now.AddSeconds(2), quotaLimit: 3));

        existing.Kind.Should().Be(ReserveQuotaResultKind.Existing);
        existing.AttemptId.Should().Be(first.AttemptId);
        conflict.Kind.Should().Be(ReserveQuotaResultKind.Conflict);
        conflict.AttemptId.Should().Be(first.AttemptId);
        conflict.ErrorCode.Should().Be("idempotency_key_reused_with_different_request");

        await using var verifyDb = fixture.CreateContext();
        (await verifyDb.RewriteAttempts.CountAsync()).Should().Be(1);
        (await verifyDb.UsageReservations.CountAsync()).Should().Be(1);
        (await verifyDb.OutboxMessages.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task ReserveQuotaAsync_uses_credit_when_period_quota_is_full_and_release_refunds_it()
    {
        await using var fixture = await QuotaUseCaseDbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var now = DateTimeOffset.Parse("2026-06-09T00:00:00Z");
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
                ExpiresAt = now.AddDays(30),
            });
            await seedDb.SaveChangesAsync();
        }

        await using (var reserveDb = fixture.CreateContext())
        {
            var reserve = await CreateReserveHandler(reserveDb).HandleAsync(
                ReserveCommand(user.Id, "idem-credit", "hash-credit", now, quotaLimit: 3));

            reserve.Kind.Should().Be(ReserveQuotaResultKind.Created);
        }

        await using (var reservedDb = fixture.CreateContext())
        {
            var period = await reservedDb.UsagePeriods.SingleAsync();
            period.UsedCount.Should().Be(3);
            period.ReservedCount.Should().Be(0);
            (await reservedDb.RewriteCredits.SingleAsync()).AmountConsumed.Should().Be(1);
            (await reservedDb.UsageReservations.SingleAsync()).RewriteCreditId.Should().NotBeNull();
        }

        Guid attemptId;
        await using (var lookupDb = fixture.CreateContext())
        {
            attemptId = await lookupDb.RewriteAttempts.Select(x => x.Id).SingleAsync();
        }

        await using (var releaseDb = fixture.CreateContext())
        {
            await CreateReleaseHandler(releaseDb).HandleAsync(new ReleaseQuotaCommand(
                attemptId,
                "provider_failed",
                now.AddMinutes(1)));
        }

        await using var verifyDb = fixture.CreateContext();
        (await verifyDb.RewriteCredits.SingleAsync()).AmountConsumed.Should().Be(0);
        (await verifyDb.UsageReservations.SingleAsync()).Status.Should().Be(UsageReservationStatus.Released);
        (await verifyDb.RewriteAttempts.SingleAsync()).Status.Should().Be(RewriteAttemptStatus.Failed);
    }

    [Fact]
    public async Task ReserveQuotaAsync_leaves_no_pending_mutation_when_period_is_full_and_no_credit_exists()
    {
        await using var fixture = await QuotaUseCaseDbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var now = DateTimeOffset.Parse("2026-06-09T00:00:00Z");
        await using (var seedDb = fixture.CreateContext())
        {
            seedDb.UsagePeriods.Add(new UsagePeriod
            {
                UserId = user.Id,
                PeriodKey = "free:lifetime",
                QuotaLimit = 3,
                UsedCount = 3,
                ReservedCount = 0,
                CreatedAt = now.AddDays(-1),
                UpdatedAt = now.AddDays(-1),
            });
            await seedDb.SaveChangesAsync();
        }

        await using var handlerDb = fixture.CreateContext();
        var result = await CreateReserveHandler(handlerDb).HandleAsync(
            ReserveCommand(user.Id, "idem-no-credit", "hash-no-credit", now, quotaLimit: 3));

        result.Kind.Should().Be(ReserveQuotaResultKind.QuotaExceeded);

        await handlerDb.SaveChangesAsync();
        await using var verifyDb = fixture.CreateContext();
        var period = await verifyDb.UsagePeriods.SingleAsync();
        period.QuotaLimit.Should().Be(3);
        period.UsedCount.Should().Be(3);
        period.ReservedCount.Should().Be(0);
        period.UpdatedAt.Should().Be(now.AddDays(-1));
        (await verifyDb.RewriteAttempts.CountAsync()).Should().Be(0);
        (await verifyDb.UsageReservations.CountAsync()).Should().Be(0);
        (await verifyDb.OutboxMessages.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ReserveQuotaAsync_uses_retrying_transaction_for_reservation_races()
    {
        await using var fixture = await QuotaUseCaseDbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var now = DateTimeOffset.Parse("2026-06-09T00:00:00Z");
        await using var handlerDb = fixture.CreateContext();
        var retryingUnitOfWork = new SimulatedRetryUnitOfWork(new UnitOfWork(handlerDb));
        var handler = CreateReserveHandler(handlerDb, retryingUnitOfWork);

        var result = await handler.HandleAsync(ReserveCommand(
            user.Id,
            "idem-retry",
            "hash-retry",
            now,
            quotaLimit: 3));

        result.Kind.Should().Be(ReserveQuotaResultKind.Created);
        retryingUnitOfWork.MaxAttemptsSeen.Should().Be(3);
        retryingUnitOfWork.TransactionAttempts.Should().Be(2);

        await using var verifyDb = fixture.CreateContext();
        (await verifyDb.RewriteAttempts.CountAsync()).Should().Be(1);
        (await verifyDb.UsageReservations.CountAsync()).Should().Be(1);
        (await verifyDb.OutboxMessages.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task FinalizeQuotaSuccessAsync_charges_period_quota_once()
    {
        await using var fixture = await QuotaUseCaseDbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var now = DateTimeOffset.Parse("2026-06-09T00:00:00Z");
        Guid attemptId;
        await using (var reserveDb = fixture.CreateContext())
        {
            var reserve = await CreateReserveHandler(reserveDb).HandleAsync(
                ReserveCommand(user.Id, "idem-success", "hash-success", now, quotaLimit: 3));
            attemptId = reserve.AttemptId;
        }

        await using (var finalizeDb = fixture.CreateContext())
        {
            var handler = CreateFinalizeHandler(finalizeDb);
            await handler.HandleAsync(new FinalizeQuotaSuccessCommand(
                attemptId,
                "{\"rewrittenText\":\"hello\"}",
                now.AddMinutes(1)));
            await handler.HandleAsync(new FinalizeQuotaSuccessCommand(
                attemptId,
                "{\"rewrittenText\":\"hello\"}",
                now.AddMinutes(2)));
        }

        await using var verifyDb = fixture.CreateContext();
        var period = await verifyDb.UsagePeriods.SingleAsync();
        period.UsedCount.Should().Be(1);
        period.ReservedCount.Should().Be(0);
        (await verifyDb.UsageReservations.SingleAsync()).Status.Should().Be(UsageReservationStatus.Finalized);
        (await verifyDb.RewriteAttempts.SingleAsync()).Status.Should().Be(RewriteAttemptStatus.Succeeded);
    }

    [Fact]
    public async Task MarkQuotaProcessingAsync_allows_only_one_pending_claim()
    {
        await using var fixture = await QuotaUseCaseDbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var now = DateTimeOffset.Parse("2026-06-09T00:00:00Z");
        Guid attemptId;
        await using (var reserveDb = fixture.CreateContext())
        {
            var reserve = await CreateReserveHandler(reserveDb).HandleAsync(
                ReserveCommand(user.Id, "idem-processing", "hash-processing", now, quotaLimit: 3));
            attemptId = reserve.AttemptId;
        }

        await using var processingDb = fixture.CreateContext();
        var handler = CreateMarkProcessingHandler(processingDb);
        var first = await handler.HandleAsync(new MarkQuotaProcessingCommand(attemptId, now.AddSeconds(10)));
        var second = await handler.HandleAsync(new MarkQuotaProcessingCommand(attemptId, now.AddSeconds(20)));

        first.Should().BeTrue();
        second.Should().BeFalse();
        await using var verifyDb = fixture.CreateContext();
        (await verifyDb.RewriteAttempts.SingleAsync()).Status.Should().Be(RewriteAttemptStatus.Processing);
    }

    [Fact]
    public async Task ReleaseExpiredReservationsAsync_drains_expired_reservations_in_batches()
    {
        await using var fixture = await QuotaUseCaseDbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var now = DateTimeOffset.Parse("2026-06-09T00:00:00Z");
        var attemptIds = new List<Guid>();
        for (var index = 0; index < 5; index++)
        {
            await using var reserveDb = fixture.CreateContext();
            var reserve = await CreateReserveHandler(reserveDb).HandleAsync(ReserveCommand(
                user.Id,
                $"idem-expired-{index}",
                $"hash-expired-{index}",
                now,
                quotaLimit: 5,
                reservationTtl: TimeSpan.FromMinutes(1)));
            attemptIds.Add(reserve.AttemptId);
        }

        await using (var processingDb = fixture.CreateContext())
        {
            await CreateMarkProcessingHandler(processingDb).HandleAsync(new MarkQuotaProcessingCommand(
                attemptIds[0],
                now.AddSeconds(10)));
        }

        int released;
        await using (var cleanupDb = fixture.CreateContext())
        {
            released = await CreateExpiredHandler(cleanupDb).HandleAsync(
                new ReleaseExpiredReservationsCommand(now.AddMinutes(2), BatchSize: 2));
        }

        released.Should().Be(5);
        await using var verifyDb = fixture.CreateContext();
        var period = await verifyDb.UsagePeriods.SingleAsync();
        period.UsedCount.Should().Be(0);
        period.ReservedCount.Should().Be(0);
        (await verifyDb.UsageReservations.CountAsync(x => x.Status == UsageReservationStatus.Expired)).Should().Be(5);
        (await verifyDb.RewriteAttempts.CountAsync(x => x.Status == RewriteAttemptStatus.Expired)).Should().Be(5);
        (await verifyDb.RewriteAttempts.CountAsync(x => x.ErrorCode == "processing_timed_out")).Should().Be(1);
        (await verifyDb.RewriteAttempts.CountAsync(x => x.ErrorCode == "reservation_expired")).Should().Be(4);
    }

    [Fact]
    public async Task ExpiredReservationForSoftDeletedAttemptStillReleased()
    {
        await using var fixture = await QuotaUseCaseDbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var now = DateTimeOffset.Parse("2026-06-09T00:00:00Z");
        var periodId = Guid.NewGuid();
        var attemptId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();

        await using (var seedDb = fixture.CreateContext())
        {
            seedDb.UsagePeriods.Add(new UsagePeriod
            {
                Id = periodId,
                UserId = user.Id,
                PeriodKey = "free:lifetime",
                QuotaLimit = 3,
                UsedCount = 0,
                ReservedCount = 1,
                CreatedAt = now.AddMinutes(-20),
                UpdatedAt = now.AddMinutes(-20),
            });
            seedDb.RewriteAttempts.Add(new RewriteAttempt
            {
                Id = attemptId,
                UserId = user.Id,
                IdempotencyKey = "idem-soft-expired",
                RequestHash = "hash-soft-expired",
                RequestJson = "{\"roughDraftReply\":\"Thanks for your message.\"}",
                Status = RewriteAttemptStatus.Processing,
                DeletedAt = now.AddMinutes(-1),
                CreatedAt = now.AddMinutes(-20),
                ExpiresAt = now.AddMinutes(-5),
            });
            seedDb.UsageReservations.Add(new UsageReservation
            {
                Id = reservationId,
                UserId = user.Id,
                UsagePeriodId = periodId,
                RewriteAttemptId = attemptId,
                Status = UsageReservationStatus.Pending,
                CreatedAt = now.AddMinutes(-20),
                ExpiresAt = now.AddMinutes(-5),
            });
            await seedDb.SaveChangesAsync();
        }

        int released;
        await using (var cleanupDb = fixture.CreateContext())
        {
            released = await CreateExpiredHandler(cleanupDb).HandleAsync(
                new ReleaseExpiredReservationsCommand(now, BatchSize: 10));
        }

        released.Should().Be(1);
        await using var verifyDb = fixture.CreateContext();
        var period = await verifyDb.UsagePeriods.SingleAsync(x => x.Id == periodId);
        period.ReservedCount.Should().Be(0);
        var reservation = await verifyDb.UsageReservations.SingleAsync(x => x.Id == reservationId);
        reservation.Status.Should().Be(UsageReservationStatus.Expired);
        reservation.ReleasedAt.Should().Be(now);
        var attempt = await verifyDb.RewriteAttempts
            .IgnoreQueryFilters()
            .SingleAsync(x => x.Id == attemptId);
        attempt.Status.Should().Be(RewriteAttemptStatus.Expired);
        attempt.ErrorCode.Should().Be("processing_timed_out");
        attempt.CompletedAt.Should().Be(now);
    }

    [Fact]
    public async Task FinalizeQuotaSuccessAsync_does_not_overwrite_expired_reservation()
    {
        await using var fixture = await QuotaUseCaseDbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var now = DateTimeOffset.Parse("2026-06-09T00:00:00Z");
        Guid attemptId;
        await using (var reserveDb = fixture.CreateContext())
        {
            var reserve = await CreateReserveHandler(reserveDb).HandleAsync(ReserveCommand(
                user.Id,
                "idem-late-success",
                "hash-late-success",
                now,
                quotaLimit: 1,
                reservationTtl: TimeSpan.FromMinutes(1)));
            attemptId = reserve.AttemptId;
        }

        await using (var cleanupDb = fixture.CreateContext())
        {
            await CreateExpiredHandler(cleanupDb).HandleAsync(new ReleaseExpiredReservationsCommand(now.AddMinutes(2)));
        }

        await using (var finalizeDb = fixture.CreateContext())
        {
            await CreateFinalizeHandler(finalizeDb).HandleAsync(new FinalizeQuotaSuccessCommand(
                attemptId,
                "{\"rewrittenText\":\"late\"}",
                now.AddMinutes(3)));
        }

        await using var verifyDb = fixture.CreateContext();
        var period = await verifyDb.UsagePeriods.SingleAsync();
        period.UsedCount.Should().Be(0);
        period.ReservedCount.Should().Be(0);
        (await verifyDb.UsageReservations.SingleAsync()).Status.Should().Be(UsageReservationStatus.Expired);
        (await verifyDb.RewriteAttempts.SingleAsync()).Status.Should().Be(RewriteAttemptStatus.Expired);
    }

    private static ReserveQuotaHandler CreateReserveHandler(
        AppDbContext db,
        IUnitOfWork? unitOfWork = null) =>
        new(
            new UsagePeriodRepository(db),
            new RewriteAttemptRepository(db),
            new UsageReservationRepository(db),
            new RewriteCreditRepository(db),
            new OutboxMessageRepository(db),
            unitOfWork ?? new UnitOfWork(db));

    private static FinalizeQuotaSuccessHandler CreateFinalizeHandler(AppDbContext db) =>
        new(
            new RewriteAttemptRepository(db),
            new UsageReservationRepository(db),
            new UsagePeriodRepository(db),
            new UnitOfWork(db));

    private static MarkQuotaProcessingHandler CreateMarkProcessingHandler(AppDbContext db) =>
        new(new RewriteAttemptRepository(db), new UnitOfWork(db));

    private static ReleaseQuotaHandler CreateReleaseHandler(AppDbContext db) =>
        new(
            new RewriteAttemptRepository(db),
            new UsageReservationRepository(db),
            new UsagePeriodRepository(db),
            new RewriteCreditRepository(db),
            new UnitOfWork(db));

    private static ReleaseExpiredReservationsHandler CreateExpiredHandler(AppDbContext db) =>
        new(new UsageReservationRepository(db), new UnitOfWork(db));

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

    private sealed class SimulatedRetryUnitOfWork(IUnitOfWork inner) : IUnitOfWork
    {
        public int MaxAttemptsSeen { get; private set; }
        public int TransactionAttempts { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken ct = default) =>
            inner.SaveChangesAsync(ct);

        public Task ExecuteInTransactionAsync(
            Func<CancellationToken, Task> operation,
            CancellationToken ct = default) =>
            inner.ExecuteInTransactionAsync(operation, ct);

        public Task ExecuteInTransactionAsync(
            Func<CancellationToken, Task> operation,
            IsolationLevel isolationLevel,
            CancellationToken ct = default) =>
            inner.ExecuteInTransactionAsync(operation, isolationLevel, ct);

        public Task<TResult> ExecuteInTransactionAsync<TResult>(
            Func<CancellationToken, Task<TResult>> operation,
            IsolationLevel isolationLevel,
            CancellationToken ct = default) =>
            inner.ExecuteInTransactionAsync(operation, isolationLevel, ct);

        public async Task<TResult> ExecuteInTransactionAsync<TResult>(
            Func<CancellationToken, Task<TResult>> operation,
            IsolationLevel isolationLevel,
            int maxAttempts,
            CancellationToken ct = default)
        {
            MaxAttemptsSeen = maxAttempts;
            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                TransactionAttempts++;
                if (attempt == 1)
                {
                    continue;
                }

                return await inner.ExecuteInTransactionAsync(operation, isolationLevel, ct);
            }

            throw new InvalidOperationException("Retry simulation did not reach a successful attempt.");
        }
    }

    private sealed class QuotaUseCaseDbFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private QuotaUseCaseDbFixture(SqliteConnection connection)
        {
            _connection = connection;
        }

        public static async Task<QuotaUseCaseDbFixture> CreateAsync()
        {
            var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();
            var fixture = new QuotaUseCaseDbFixture(connection);
            await using var db = fixture.CreateContext();
            await db.Database.EnsureCreatedAsync();
            return fixture;
        }

        public AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(_connection)
                .EnableSensitiveDataLogging()
                .Options;
            return new AppDbContext(options);
        }

        public async Task<AppUser> CreateUserAsync()
        {
            await using var db = CreateContext();
            var user = new AppUser
            {
                ExternalAuthUserId = $"clerk_{Guid.NewGuid():N}",
                Email = "test@example.com",
                SubscriptionStatus = SubscriptionStatus.Inactive,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            };
            db.AppUsers.Add(user);
            await db.SaveChangesAsync();
            return user;
        }

        public async ValueTask DisposeAsync()
        {
            await _connection.DisposeAsync();
        }
    }
}
