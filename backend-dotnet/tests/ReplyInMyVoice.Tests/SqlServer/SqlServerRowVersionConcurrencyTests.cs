using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Infrastructure.Repositories;

namespace ReplyInMyVoice.Tests.SqlServer;

[Collection(SqlServerCollection.Name)]
[Trait("Category", "SqlServer")]
public sealed class SqlServerRowVersionConcurrencyTests(SqlServerDbFixture fixture)
{
    [Fact]
    public async Task Stale_usage_period_save_throws_after_row_version_changes()
    {
        var now = DateTimeOffset.Parse("2026-06-13T12:00:00Z");
        var user = await fixture.CreateUserAsync();
        var periodId = await SeedUsagePeriodAsync(user.Id, now);

        await using var firstDb = fixture.CreateContext();
        await using var secondDb = fixture.CreateContext();
        var first = await firstDb.UsagePeriods.SingleAsync(x => x.Id == periodId);
        var second = await secondDb.UsagePeriods.SingleAsync(x => x.Id == periodId);
        var originalRowVersion = first.RowVersion;

        first.ReservedCount = 1;
        first.UpdatedAt = now.AddMinutes(1);
        await firstDb.SaveChangesAsync();
        first.RowVersion.Should().NotBe(originalRowVersion);

        second.UsedCount = 1;
        second.UpdatedAt = now.AddMinutes(2);
        var staleSave = () => secondDb.SaveChangesAsync();

        await staleSave.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    [Fact]
    public async Task TryReserveSlotAsync_stamps_fresh_row_version_on_sql_server()
    {
        var now = DateTimeOffset.Parse("2026-06-13T12:00:00Z");
        var updatedAt = now.AddMinutes(5);
        var user = await fixture.CreateUserAsync();
        var periodId = await SeedUsagePeriodAsync(user.Id, now);

        Guid originalRowVersion;
        await using (var seedDb = fixture.CreateContext())
        {
            originalRowVersion = await seedDb.UsagePeriods
                .Where(x => x.Id == periodId)
                .Select(x => x.RowVersion)
                .SingleAsync();
        }

        await using (var updateDb = fixture.CreateContext())
        {
            var repository = new UsagePeriodRepository(updateDb);

            var affected = await repository.TryReserveSlotAsync(periodId, quotaLimit: 3, updatedAt);

            affected.Should().Be(1);
        }

        await using var verifyDb = fixture.CreateContext();
        var updated = await verifyDb.UsagePeriods.SingleAsync(x => x.Id == periodId);
        updated.ReservedCount.Should().Be(1);
        updated.UsedCount.Should().Be(1);
        updated.QuotaLimit.Should().Be(3);
        updated.UpdatedAt.Should().Be(updatedAt);
        updated.RowVersion.Should().NotBe(originalRowVersion);
    }

    private async Task<Guid> SeedUsagePeriodAsync(Guid userId, DateTimeOffset now)
    {
        await using var db = fixture.CreateContext();
        var period = new UsagePeriod
        {
            UserId = userId,
            PeriodKey = "free:lifetime",
            QuotaLimit = 2,
            UsedCount = 1,
            ReservedCount = 0,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.UsagePeriods.Add(period);
        await db.SaveChangesAsync();
        return period.Id;
    }
}
