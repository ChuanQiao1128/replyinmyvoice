using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Infrastructure.Data;
using ReplyInMyVoice.Infrastructure.Repositories;

namespace ReplyInMyVoice.Tests;

public sealed class DeadLetterMessageRepositoryTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        await using var db = CreateContext();
        await db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
    }

    [Fact]
    public void DeadLetterMessageModel_has_required_columns_constraints_and_indexes()
    {
        using var db = CreateContext();

        var entity = db.Model.FindEntityType(typeof(DeadLetterMessage));

        entity.Should().NotBeNull();
        entity!.GetTableName().Should().Be("DeadLetterMessages");
        entity.FindProperty(nameof(DeadLetterMessage.Id)).Should().NotBeNull();
        entity.FindProperty(nameof(DeadLetterMessage.SourceType))!.GetMaxLength().Should().Be(40);
        entity.FindProperty(nameof(DeadLetterMessage.SourceId))!.GetMaxLength().Should().Be(160);
        entity.FindProperty(nameof(DeadLetterMessage.SourceData))!.IsNullable.Should().BeFalse();
        entity.FindProperty(nameof(DeadLetterMessage.FailureReason))!.GetMaxLength().Should().Be(1000);
        entity.FindProperty(nameof(DeadLetterMessage.CreatedAt)).Should().NotBeNull();
        entity.FindProperty(nameof(DeadLetterMessage.RequeuedAt)).Should().NotBeNull();
        entity.FindProperty(nameof(DeadLetterMessage.RowVersion))!.IsConcurrencyToken.Should().BeTrue();

        entity.GetIndexes()
            .Any(index => index.Properties.Select(x => x.Name).SequenceEqual(new[]
            {
                nameof(DeadLetterMessage.SourceType),
                nameof(DeadLetterMessage.CreatedAt),
            }))
            .Should()
            .BeTrue();
        entity.GetIndexes()
            .Any(index =>
                index.Properties.Select(x => x.Name).SequenceEqual(new[]
                {
                    nameof(DeadLetterMessage.SourceType),
                    nameof(DeadLetterMessage.RequeuedAt),
                }) &&
                string.Equals(index.GetFilter(), "[RequeuedAt] IS NULL", StringComparison.Ordinal))
            .Should()
            .BeTrue();
    }

    [Fact]
    public async Task AddAsync_is_idempotent_for_same_unrequeued_source()
    {
        await using var db = CreateContext();
        var repository = new DeadLetterMessageRepository(db);
        var now = DateTimeOffset.Parse("2026-06-19T00:00:00Z");

        await repository.AddAsync(CreateDeadLetter("OutboxMessage", "outbox-1", now));
        await db.SaveChangesAsync();
        await repository.AddAsync(CreateDeadLetter("OutboxMessage", "outbox-1", now.AddSeconds(1)));
        await db.SaveChangesAsync();

        var rows = await db.DeadLetterMessages.ToListAsync();
        rows.Should().ContainSingle();
        rows[0].FailureReason.Should().Be("terminal failure");
    }

    [Fact]
    public void AddDeadLetterMessagesMigration_exists_and_creates_table_and_indexes()
    {
        var migrationsDirectory = FindBackendRoot()
            .GetDirectories("src", SearchOption.TopDirectoryOnly)
            .Single()
            .GetDirectories("ReplyInMyVoice.Infrastructure", SearchOption.TopDirectoryOnly)
            .Single()
            .GetDirectories("Migrations", SearchOption.TopDirectoryOnly)
            .Single();

        var migration = migrationsDirectory
            .EnumerateFiles("202606*_AddDeadLetterMessages.cs")
            .Should()
            .ContainSingle()
            .Subject;

        var text = File.ReadAllText(migration.FullName);
        text.Should().Contain("CreateTable(");
        text.Should().Contain("DeadLetterMessages");
        text.Should().Contain("SourceType");
        text.Should().Contain("SourceId");
        text.Should().Contain("SourceData");
        text.Should().Contain("FailureReason");
        text.Should().Contain("RequeuedAt");
        text.Should().Contain("RowVersion");
        text.Should().Contain("IX_DeadLetterMessages_SourceType_CreatedAt");
        text.Should().Contain("IX_DeadLetterMessages_SourceType_RequeuedAt");
    }

    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new AppDbContext(options);
    }

    private static DeadLetterMessage CreateDeadLetter(
        string sourceType,
        string sourceId,
        DateTimeOffset createdAt) =>
        new()
        {
            SourceType = sourceType,
            SourceId = sourceId,
            SourceData = $$"""{"sourceType":"{{sourceType}}","sourceId":"{{sourceId}}","attemptCount":3,"lastError":"terminal failure"}""",
            FailureReason = "terminal failure",
            CreatedAt = createdAt,
        };

    private static DirectoryInfo FindBackendRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (directory.GetFiles("ReplyInMyVoice.sln").Length == 1)
            {
                return directory;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate backend-dotnet root.");
    }
}
