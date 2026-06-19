using System.Text.Json;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ReplyInMyVoice.Functions.Functions;
using ReplyInMyVoice.Infrastructure.Data;
using ReplyInMyVoice.Infrastructure.Services;
using Stripe;

namespace ReplyInMyVoice.Tests;

public sealed class HealthFunctionReadinessTests
{
    private const string ServiceBusConnectionString =
        "Endpoint=sb://other.servicebus.windows.net/;SharedAccessKeyName=x;SharedAccessKey=y";

    [Fact]
    public async Task Readiness_reports_service_bus_managed_identity_auth_mode()
    {
        await using var connection = await OpenSqliteConnectionAsync();
        await using var db = await CreateContextAsync(connection);
        await using var client = new ServiceBusClient(
            "rimv-test.servicebus.windows.net",
            new DefaultAzureCredential());
        await using var sender = client.CreateSender("rewrite-jobs");
        var function = new HealthFunction(
            db,
            BuildConfiguration(new Dictionary<string, string?>
            {
                ["USE_MANAGED_IDENTITY"] = "true",
                ["ServiceBus:fullyQualifiedNamespace"] = "rimv-test.servicebus.windows.net",
            }),
            sender,
            new PassingStripeAuthProbe());

        var result = await function.ReadinessHealth(
            new DefaultHttpContext().Request,
            CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var json = JsonSerializer.Serialize(okResult.Value);
        using var document = JsonDocument.Parse(json);
        var serviceBus = document.RootElement.GetProperty("checks").GetProperty("serviceBus");
        serviceBus.GetProperty("ok").GetBoolean().Should().BeTrue();
        serviceBus.GetProperty("configured").GetBoolean().Should().BeTrue();
        serviceBus.GetProperty("senderResolved").GetBoolean().Should().BeTrue();
        serviceBus.GetProperty("authMode").GetString().Should().Be("managed_identity");
    }

    [Fact]
    public async Task Readiness_reports_connection_string_auth_mode()
    {
        await using var connection = await OpenSqliteConnectionAsync();
        await using var db = await CreateContextAsync(connection);
        await using var client = new ServiceBusClient(ServiceBusConnectionString);
        await using var sender = client.CreateSender("rewrite-jobs");
        var function = new HealthFunction(
            db,
            BuildConfiguration(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ServiceBus"] = ServiceBusConnectionString,
            }),
            sender,
            new PassingStripeAuthProbe());

        var result = await function.ReadinessHealth(
            new DefaultHttpContext().Request,
            CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var json = JsonSerializer.Serialize(okResult.Value);
        using var document = JsonDocument.Parse(json);
        var serviceBus = document.RootElement.GetProperty("checks").GetProperty("serviceBus");
        serviceBus.GetProperty("ok").GetBoolean().Should().BeTrue();
        serviceBus.GetProperty("configured").GetBoolean().Should().BeTrue();
        serviceBus.GetProperty("authMode").GetString().Should().Be("connection_string");
    }

    private static async Task<SqliteConnection> OpenSqliteConnectionAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        return connection;
    }

    private static async Task<AppDbContext> CreateContextAsync(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static IConfiguration BuildConfiguration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Concat(new Dictionary<string, string?>
            {
                ["STRIPE_SECRET_KEY"] = "configured_test_key",
            }))
            .Build();

    private sealed class PassingStripeAuthProbe : IStripeAuthProbe
    {
        public Task<bool> VerifyAuthenticatedAsync(
            IStripeClient client,
            CancellationToken cancellationToken) =>
            Task.FromResult(true);
    }
}
