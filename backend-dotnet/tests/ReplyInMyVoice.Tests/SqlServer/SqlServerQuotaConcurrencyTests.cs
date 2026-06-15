using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ReplyInMyVoice.Application.Common;
using ReplyInMyVoice.Application.UseCases.Quota;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Infrastructure.Data;
using ReplyInMyVoice.Infrastructure.Repositories;

namespace ReplyInMyVoice.Tests.SqlServer;

[Collection(SqlServerCollection.Name)]
[Trait("Category", "SqlServer")]
public sealed class SqlServerQuotaConcurrencyTests(SqlServerDbFixture fixture)
{
    [Fact]
    public async Task Parallel_reserves_with_one_period_slot_remaining_grant_exactly_one()
    {
        const int requestCount = 8;
        var now = DateTimeOffset.Parse("2026-06-13T12:00:00Z");
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
                    $"sqlserver-period-race-{index}",
                    $"hash-sqlserver-period-race-{index}",
                    now,
                    quotaLimit: 3));
            }))
            .ToArray();

        start.SetResult();
        var results = await Task.WhenAll(tasks);

        results.Count(x => x.Kind == ReserveQuotaResultKind.Created).Should().Be(1);
        results.Count(x => x.Kind == ReserveQuotaResultKind.QuotaExceeded).Should().Be(requestCount - 1);

        await using var verifyDb = fixture.CreateContext();
        var period = await verifyDb.UsagePeriods.SingleAsync(x => x.UserId == user.Id);
        period.ReservedCount.Should().Be(1);
        period.UsedCount.Should().Be(2);
        var attemptIds = await verifyDb.RewriteAttempts
            .Where(x => x.UserId == user.Id)
            .Select(x => x.Id.ToString())
            .ToListAsync();
        (await verifyDb.RewriteAttempts.CountAsync(x => x.UserId == user.Id)).Should().Be(1);
        (await verifyDb.UsageReservations.CountAsync(x => x.UserId == user.Id)).Should().Be(1);
        (await verifyDb.OutboxMessages.CountAsync(x =>
            x.CorrelationId != null &&
            attemptIds.Contains(x.CorrelationId))).Should().Be(1);
    }

    [Fact]
    public async Task Parallel_reserves_with_one_credit_remaining_consume_exactly_one()
    {
        const int requestCount = 8;
        var now = DateTimeOffset.Parse("2026-06-13T12:00:00Z");
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
                    $"sqlserver-credit-race-{index}",
                    $"hash-sqlserver-credit-race-{index}",
                    now,
                    quotaLimit: 3));
            }))
            .ToArray();

        start.SetResult();
        var results = await Task.WhenAll(tasks);

        results.Count(x => x.Kind == ReserveQuotaResultKind.Created).Should().Be(1);
        results.Count(x => x.Kind == ReserveQuotaResultKind.QuotaExceeded).Should().Be(requestCount - 1);

        await using var verifyDb = fixture.CreateContext();
        var period = await verifyDb.UsagePeriods.SingleAsync(x => x.UserId == user.Id);
        period.ReservedCount.Should().Be(0);
        (await verifyDb.RewriteCredits.SingleAsync(x => x.UserId == user.Id)).AmountConsumed.Should().Be(1);
        (await verifyDb.UsageReservations.SingleAsync(x => x.UserId == user.Id)).RewriteCreditId.Should().NotBeNull();
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

    private static ReserveQuotaCommand ReserveCommand(
        Guid userId,
        string idempotencyKey,
        string requestHash,
        DateTimeOffset now,
        int quotaLimit) =>
        new(
            userId,
            idempotencyKey,
            requestHash,
            "{\"roughDraftReply\":\"Thanks for your message. I will reply soon.\",\"tone\":\"warm\"}",
            "free:lifetime",
            quotaLimit,
            now,
            TimeSpan.FromMinutes(10),
            ApiKeyId: null);
}
