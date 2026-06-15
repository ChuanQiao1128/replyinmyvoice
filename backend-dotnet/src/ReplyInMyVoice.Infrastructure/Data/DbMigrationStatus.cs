using System.Data;
using System.Globalization;
using Microsoft.EntityFrameworkCore;

namespace ReplyInMyVoice.Infrastructure.Data;

public static class DbMigrationStatus
{
    public static async Task<MigrationStatusResult> EvaluateAsync(
        DbContext db,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!db.Database.IsRelational())
            {
                return MigrationStatusResult.NoPending();
            }

            var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToArray();
            if (pending.Length > 0 && IsSqlite(db) && await HasSqliteSchemaAsync(db, cancellationToken))
            {
                pending = [];
            }

            return new MigrationStatusResult(
                pending.Length > 0,
                pending,
                Error: null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new MigrationStatusResult(
                HasPendingMigrations: false,
                PendingMigrations: [],
                Error: ex.GetType().Name);
        }
    }

    private static bool IsSqlite(DbContext db) =>
        db.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true;

    private static async Task<bool> HasSqliteSchemaAsync(DbContext db, CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        var shouldClose = connection.State == ConnectionState.Closed;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT COUNT(*)
                FROM sqlite_master
                WHERE type = 'table'
                    AND name NOT LIKE 'sqlite_%'
                    AND name <> '__EFMigrationsHistory'
                """;
            var result = await command.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt32(result, CultureInfo.InvariantCulture) > 0;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }
}

public sealed record MigrationStatusResult(
    bool HasPendingMigrations,
    IReadOnlyList<string> PendingMigrations,
    string? Error)
{
    public static MigrationStatusResult NoPending() =>
        new(
            HasPendingMigrations: false,
            PendingMigrations: [],
            Error: null);
}
