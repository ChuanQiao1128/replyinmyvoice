using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Infrastructure.Data;
using Testcontainers.MsSql;

namespace ReplyInMyVoice.Tests.SqlServer;

public sealed class SqlServerDbFixture : IAsyncLifetime
{
    private const string TestContainerPassword = "ReplyInMyVoice_TestOnly_123!";

    private MsSqlContainer? _container;
    private string? _connectionString;

    public async Task InitializeAsync()
    {
        var baseConnectionString = Environment.GetEnvironmentVariable("RIMV_SQLSERVER_TEST_CONNECTION")
            ?? Environment.GetEnvironmentVariable("SQLSERVER_TEST_CONNECTION");

        if (string.IsNullOrWhiteSpace(baseConnectionString))
        {
            _container = new MsSqlBuilder()
                .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
                .WithPassword(TestContainerPassword)
                .Build();

            await _container.StartAsync();
            baseConnectionString = _container.GetConnectionString();
        }

        _connectionString = CreateDatabaseConnectionString(baseConnectionString);
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (_connectionString is not null)
        {
            await using var db = CreateContext();
            await db.Database.EnsureDeletedAsync();
        }

        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    public AppDbContext CreateContext()
    {
        if (_connectionString is null)
        {
            throw new InvalidOperationException("SQL Server fixture has not been initialized.");
        }

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(_connectionString)
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
            ExternalAuthUserId = $"clerk_sqlserver_{Guid.NewGuid():N}",
            Email = "sqlserver-test@example.com",
            SubscriptionStatus = SubscriptionStatus.Inactive,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.AppUsers.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private static string CreateDatabaseConnectionString(string connectionString)
    {
        var builder = new SqlConnectionStringBuilder(connectionString)
        {
            InitialCatalog = $"ReplyInMyVoiceSqlServerTests_{Guid.NewGuid():N}",
            Encrypt = true,
            TrustServerCertificate = true,
            ConnectTimeout = 30,
        };

        return builder.ConnectionString;
    }
}
