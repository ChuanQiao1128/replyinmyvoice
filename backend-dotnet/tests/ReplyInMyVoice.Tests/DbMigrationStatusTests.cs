using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Infrastructure.Data;

namespace ReplyInMyVoice.Tests;

public sealed class DbMigrationStatusTests
{
    [Fact]
    public async Task EvaluateAsync_returns_no_pending_migrations_for_sqlite_schema_created_without_migration_history()
    {
        await using var connection = await OpenSqliteConnectionAsync();
        await using var db = CreateContext(connection);
        await db.Database.EnsureCreatedAsync();

        var result = await DbMigrationStatus.EvaluateAsync(db, CancellationToken.None);

        result.HasPendingMigrations.Should().BeFalse();
        result.PendingMigrations.Should().BeEmpty();
        result.Error.Should().BeNull();
    }

    [Fact]
    public async Task EvaluateAsync_reports_pending_migrations_when_sqlite_schema_has_not_been_created()
    {
        await using var connection = await OpenSqliteConnectionAsync();
        await using var db = CreateContext(connection);

        var result = await DbMigrationStatus.EvaluateAsync(db, CancellationToken.None);

        result.HasPendingMigrations.Should().BeTrue();
        result.PendingMigrations.Should().NotBeEmpty();
        result.Error.Should().BeNull();
    }

    [Fact]
    public async Task EvaluateAsync_captures_errors_without_throwing()
    {
        await using var connection = await OpenSqliteConnectionAsync();
        var db = CreateContext(connection);
        await db.DisposeAsync();

        var result = await DbMigrationStatus.EvaluateAsync(db, CancellationToken.None);

        result.HasPendingMigrations.Should().BeFalse();
        result.PendingMigrations.Should().BeEmpty();
        result.Error.Should().NotBeNullOrWhiteSpace();
    }

    private static async Task<SqliteConnection> OpenSqliteConnectionAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        return connection;
    }

    private static AppDbContext CreateContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        return new AppDbContext(options);
    }
}
