using System.Reflection;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using ReplyInMyVoice.Infrastructure.Data;

namespace ReplyInMyVoice.Tests;

public sealed class FreeBaselineMigrationTests
{
    [Fact]
    public void FreeBaselineZeroMigration_updates_existing_free_lifetime_periods()
    {
        var migrationType = FindFreeBaselineZeroMigrationType();

        var upOperations = BuildOperations(migrationType, "Up");

        var sql = upOperations.OfType<SqlOperation>()
            .Should()
            .ContainSingle(operation =>
                operation.Sql.Contains("UsagePeriods", StringComparison.Ordinal) &&
                operation.Sql.Contains("QuotaLimit", StringComparison.Ordinal) &&
                operation.Sql.Contains("free:lifetime", StringComparison.Ordinal))
            .Subject
            .Sql;
        sql.Should().Contain("UPDATE");
        sql.Should().Contain("QuotaLimit] = 0");
        sql.Should().Contain("UpdatedAt");
        sql.Should().Contain("PeriodKey] = 'free:lifetime'");
    }

    [Fact]
    public void FreeBaselineZeroMigration_down_is_forward_only()
    {
        var migrationType = FindFreeBaselineZeroMigrationType();

        var downOperations = BuildOperations(migrationType, "Down");

        downOperations.Should().BeEmpty();
    }

    private static Type FindFreeBaselineZeroMigrationType()
    {
        var migrationType = typeof(AppDbContext).Assembly
            .GetTypes()
            .SingleOrDefault(type =>
                typeof(Migration).IsAssignableFrom(type) &&
                type.Name.Contains("FreeBaselineZero", StringComparison.Ordinal));

        migrationType.Should().NotBeNull("PROMO-06 requires a forward-only EF migration for old free rows");
        return migrationType!;
    }

    private static IReadOnlyList<MigrationOperation> BuildOperations(Type migrationType, string methodName)
    {
        var migration = (Migration)Activator.CreateInstance(migrationType)!;
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        var method = migrationType.GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        method.Should().NotBeNull();

        method!.Invoke(migration, new object[] { builder });
        return builder.Operations;
    }
}
