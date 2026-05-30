using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Infrastructure.Data;

namespace ReplyInMyVoice.Tests;

public sealed class AdminAuditLogTests : IAsyncLifetime
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
    public async Task AdminAuditLogPersists()
    {
        var id = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();
        var createdAt = DateTimeOffset.Parse("2026-05-30T12:34:56+00:00");

        await using (var db = CreateContext())
        {
            db.AdminAuditLogs.Add(new AdminAuditLog
            {
                Id = id,
                AdminExternalAuthUserId = "admin-oid-123",
                AdminEmail = "admin@example.com",
                Action = "credits.adjust",
                TargetUserId = targetUserId,
                DetailsJson = "{\"amount\":5}",
                CreatedAt = createdAt,
            });
            await db.SaveChangesAsync();
        }

        await using (var db = CreateContext())
        {
            var saved = await db.AdminAuditLogs.SingleAsync(x => x.Id == id);

            saved.AdminExternalAuthUserId.Should().Be("admin-oid-123");
            saved.AdminEmail.Should().Be("admin@example.com");
            saved.Action.Should().Be("credits.adjust");
            saved.TargetUserId.Should().Be(targetUserId);
            saved.DetailsJson.Should().Be("{\"amount\":5}");
            saved.CreatedAt.Should().Be(createdAt);
        }
    }

    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new AppDbContext(options);
    }
}
