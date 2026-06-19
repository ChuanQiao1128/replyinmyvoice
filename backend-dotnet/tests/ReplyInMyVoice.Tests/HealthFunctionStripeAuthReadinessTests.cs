using System.Net;
using System.Text.Json;
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

public sealed class HealthFunctionStripeAuthReadinessTests
{
    private const string ServiceBusConnectionString =
        "Endpoint=sb://other.servicebus.windows.net/;SharedAccessKeyName=x;SharedAccessKey=y";

    [Fact]
    public async Task StripeAuth_ok_when_valid_key()
    {
        await using var connection = await OpenSqliteConnectionAsync();
        await using var db = await CreateContextAsync(connection);
        await using var client = new ServiceBusClient(ServiceBusConnectionString);
        await using var sender = client.CreateSender("rewrite-jobs");
        var function = new HealthFunction(
            db,
            BuildConfiguration(),
            sender,
            new FakeStripeAuthProbe(isAuthenticated: true));

        var result = await function.ReadinessHealth(
            new DefaultHttpContext().Request,
            CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var json = JsonSerializer.Serialize(okResult.Value);
        using var document = JsonDocument.Parse(json);
        document.RootElement.GetProperty("ok").GetBoolean().Should().BeTrue();
        document.RootElement.GetProperty("status").GetString().Should().Be("ready");

        var stripeAuth = document.RootElement.GetProperty("checks").GetProperty("stripeAuth");
        stripeAuth.GetProperty("ok").GetBoolean().Should().BeTrue();
        stripeAuth.GetProperty("authMode").GetString().Should().Be("secret_key");
        stripeAuth.GetProperty("error").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task StripeAuth_fails_when_secret_key_is_invalid()
    {
        await using var connection = await OpenSqliteConnectionAsync();
        await using var db = await CreateContextAsync(connection);
        await using var client = new ServiceBusClient(ServiceBusConnectionString);
        await using var sender = client.CreateSender("rewrite-jobs");
        var function = new HealthFunction(
            db,
            BuildConfiguration(),
            sender,
            new FakeStripeAuthProbe(
                failure: new StripeException(
                    HttpStatusCode.Unauthorized,
                    new StripeError { Type = "invalid_request_error" },
                    "authentication failed")));

        var result = await function.ReadinessHealth(
            new DefaultHttpContext().Request,
            CancellationToken.None);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
        var json = JsonSerializer.Serialize(objectResult.Value);
        using var document = JsonDocument.Parse(json);
        document.RootElement.GetProperty("ok").GetBoolean().Should().BeFalse();
        document.RootElement.GetProperty("status").GetString().Should().Be("degraded");

        var stripeAuth = document.RootElement.GetProperty("checks").GetProperty("stripeAuth");
        stripeAuth.GetProperty("ok").GetBoolean().Should().BeFalse();
        stripeAuth.GetProperty("authMode").GetString().Should().Be("secret_key");
        stripeAuth.GetProperty("error").GetString().Should().Be("auth_failed");
    }

    [Fact]
    public async Task StripeAuth_returns_degraded_status_in_readiness_when_auth_fails()
    {
        await using var connection = await OpenSqliteConnectionAsync();
        await using var db = await CreateContextAsync(connection);
        await using var client = new ServiceBusClient(ServiceBusConnectionString);
        await using var sender = client.CreateSender("rewrite-jobs");
        var function = new HealthFunction(
            db,
            BuildConfiguration(),
            sender,
            new FakeStripeAuthProbe(isAuthenticated: false));

        var result = await function.ReadinessHealth(
            new DefaultHttpContext().Request,
            CancellationToken.None);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
        var json = JsonSerializer.Serialize(objectResult.Value);
        using var document = JsonDocument.Parse(json);
        document.RootElement.GetProperty("ok").GetBoolean().Should().BeFalse();
        document.RootElement.GetProperty("status").GetString().Should().Be("degraded");

        var checks = document.RootElement.GetProperty("checks");
        checks.GetProperty("database").GetProperty("ok").GetBoolean().Should().BeTrue();
        checks.GetProperty("serviceBus").GetProperty("ok").GetBoolean().Should().BeTrue();
        checks.GetProperty("failedStripeEvents").GetProperty("ok").GetBoolean().Should().BeTrue();
        checks.GetProperty("lastProcessedStripeEvent").GetProperty("ok").GetBoolean().Should().BeTrue();
        checks.GetProperty("outboxBacklog").GetProperty("ok").GetBoolean().Should().BeTrue();
        checks.GetProperty("stuckReservations").GetProperty("ok").GetBoolean().Should().BeTrue();
        checks.GetProperty("stripeAuth").GetProperty("ok").GetBoolean().Should().BeFalse();
        checks.GetProperty("stripeAuth").GetProperty("error").GetString().Should().Be("invalid_request");
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

    private static IConfiguration BuildConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ServiceBus"] = ServiceBusConnectionString,
                ["STRIPE_SECRET_KEY"] = "configured_test_key",
            })
            .Build();

    private sealed class FakeStripeAuthProbe(
        bool isAuthenticated = true,
        Exception? failure = null) : IStripeAuthProbe
    {
        public Task<bool> VerifyAuthenticatedAsync(
            IStripeClient client,
            CancellationToken cancellationToken)
        {
            if (failure is not null)
            {
                throw failure;
            }

            return Task.FromResult(isAuthenticated);
        }
    }
}
