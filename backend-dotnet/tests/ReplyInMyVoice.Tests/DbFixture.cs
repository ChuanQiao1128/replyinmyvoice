using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Infrastructure.Data;

namespace ReplyInMyVoice.Tests;

internal sealed class DbFixture : IAsyncDisposable
{
    private readonly SqliteConnection _connection;

    private DbFixture(SqliteConnection connection)
    {
        _connection = connection;
    }

    public static async Task<DbFixture> CreateAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var fixture = new DbFixture(connection);
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
