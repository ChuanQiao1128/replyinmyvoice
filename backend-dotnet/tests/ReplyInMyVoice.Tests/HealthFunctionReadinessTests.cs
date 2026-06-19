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
using ReplyInMyVoice.Infrastructure.Providers;
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
            CreateApplicationStripeProbe(new FakeLegacyStripeBillingClient()));

        var result = await function.ReadinessHealth(
            new DefaultHttpContext().Request,
            CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var json = JsonSerializer.Serialize(okResult.Value);
        using var document = JsonDocument.Parse(json);
        document.RootElement.GetProperty("ok").GetBoolean().Should().BeTrue();
        var checks = document.RootElement.GetProperty("checks");
        var serviceBus = checks.GetProperty("serviceBus");
        serviceBus.GetProperty("ok").GetBoolean().Should().BeTrue();
        serviceBus.GetProperty("configured").GetBoolean().Should().BeTrue();
        serviceBus.GetProperty("senderResolved").GetBoolean().Should().BeTrue();
        serviceBus.GetProperty("authMode").GetString().Should().Be("managed_identity");
        var migrations = checks.GetProperty("migrations");
        migrations.GetProperty("ok").GetBoolean().Should().BeTrue();
        migrations.GetProperty("pendingCount").GetInt32().Should().Be(0);
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
            CreateApplicationStripeProbe(new FakeLegacyStripeBillingClient()));

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

    [Fact]
    public async Task Readiness_validates_stripe_authentication_success()
    {
        await using var connection = await OpenSqliteConnectionAsync();
        await using var db = await CreateContextAsync(connection);
        await using var client = new ServiceBusClient(ServiceBusConnectionString);
        await using var sender = client.CreateSender("rewrite-jobs");
        var stripeBillingClient = new FakeLegacyStripeBillingClient();
        var stripeProbe = CreateApplicationStripeProbe(stripeBillingClient);
        var function = new HealthFunction(
            db,
            BuildConfiguration(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ServiceBus"] = ServiceBusConnectionString,
            }),
            sender,
            stripeProbe);

        var result = await function.ReadinessHealth(
            new DefaultHttpContext().Request,
            CancellationToken.None);

        stripeBillingClient.AuthenticationCalls.Should().Be(1);
        stripeBillingClient.LastStripeClient.Should().NotBeNull();
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var json = JsonSerializer.Serialize(okResult.Value);
        using var document = JsonDocument.Parse(json);
        document.RootElement.GetProperty("ok").GetBoolean().Should().BeTrue();
        var stripeAuthentication = document.RootElement
            .GetProperty("checks")
            .GetProperty("stripeAuthentication");
        stripeAuthentication.GetProperty("ok").GetBoolean().Should().BeTrue();
        stripeAuthentication.GetProperty("authMode").GetString().Should().Be("secret_key");
        stripeAuthentication.GetProperty("error").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Readiness_reports_stripe_authentication_failure()
    {
        await using var connection = await OpenSqliteConnectionAsync();
        await using var db = await CreateContextAsync(connection);
        await using var client = new ServiceBusClient(ServiceBusConnectionString);
        await using var sender = client.CreateSender("rewrite-jobs");
        var stripeBillingClient = new FakeLegacyStripeBillingClient(new StripeException("auth failed"));
        var stripeProbe = CreateApplicationStripeProbe(stripeBillingClient);
        var function = new HealthFunction(
            db,
            BuildConfiguration(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ServiceBus"] = ServiceBusConnectionString,
            }),
            sender,
            stripeProbe);

        var result = await function.ReadinessHealth(
            new DefaultHttpContext().Request,
            CancellationToken.None);

        stripeBillingClient.AuthenticationCalls.Should().Be(1);
        stripeBillingClient.LastStripeClient.Should().NotBeNull();
        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
        var json = JsonSerializer.Serialize(objectResult.Value);
        using var document = JsonDocument.Parse(json);
        document.RootElement.GetProperty("ok").GetBoolean().Should().BeFalse();
        var stripeAuthentication = document.RootElement
            .GetProperty("checks")
            .GetProperty("stripeAuthentication");
        stripeAuthentication.GetProperty("ok").GetBoolean().Should().BeFalse();
        stripeAuthentication.GetProperty("authMode").GetString().Should().Be("secret_key");
        stripeAuthentication.GetProperty("error").GetString().Should().Be("stripe_auth_failed");
    }

    [Fact]
    public async Task Readiness_skips_stripe_probe_when_gated()
    {
        await using var connection = await OpenSqliteConnectionAsync();
        await using var db = await CreateContextAsync(connection);
        await using var client = new ServiceBusClient(ServiceBusConnectionString);
        await using var sender = client.CreateSender("rewrite-jobs");
        var stripeBillingClient = new FakeLegacyStripeBillingClient(new StripeException("should not run"));
        var stripeProbe = CreateApplicationStripeProbe(stripeBillingClient);
        var function = new HealthFunction(
            db,
            BuildConfiguration(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ServiceBus"] = ServiceBusConnectionString,
                ["Health:SkipStripeProbe"] = "true",
            }),
            sender,
            stripeProbe);

        var result = await function.ReadinessHealth(
            new DefaultHttpContext().Request,
            CancellationToken.None);

        stripeBillingClient.AuthenticationCalls.Should().Be(0);
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var json = JsonSerializer.Serialize(okResult.Value);
        using var document = JsonDocument.Parse(json);
        document.RootElement.GetProperty("ok").GetBoolean().Should().BeTrue();
        var stripeAuthentication = document.RootElement
            .GetProperty("checks")
            .GetProperty("stripeAuthentication");
        stripeAuthentication.GetProperty("ok").GetBoolean().Should().BeTrue();
        stripeAuthentication.GetProperty("authMode").GetString().Should().Be("probe_disabled");
        stripeAuthentication.GetProperty("error").ValueKind.Should().Be(JsonValueKind.Null);
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
            .AddInMemoryCollection(values)
            .Build();

    private static ApplicationStripeBillingClient CreateApplicationStripeProbe(
        FakeLegacyStripeBillingClient stripeBillingClient) =>
        new(
            BuildConfiguration(new Dictionary<string, string?>
            {
                ["STRIPE_SECRET_KEY"] = string.Concat("sk_", "test_", "readiness_probe"),
            }),
            stripeBillingClient);

    private sealed class FakeLegacyStripeBillingClient(
        Exception? authenticationException = null) : IStripeBillingClient
    {
        public int AuthenticationCalls { get; private set; }

        public IStripeClient? LastStripeClient { get; private set; }

        public Task<StripeCheckoutSessionResult> CreateCheckoutSessionAsync(
            StripeCheckoutSessionCreateRequest request,
            IStripeClient stripeClient,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<StripePortalSessionResult> CreatePortalSessionAsync(
            string customerId,
            string returnUrl,
            IStripeClient stripeClient,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task CancelSubscriptionAsync(
            string stripeSubscriptionId,
            IStripeClient stripeClient,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<StripeRefundResult> RefundPaymentAsync(
            StripeRefundRequest request,
            IStripeClient stripeClient,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task ValidateAuthenticationAsync(
            IStripeClient stripeClient,
            CancellationToken cancellationToken)
        {
            AuthenticationCalls++;
            LastStripeClient = stripeClient;

            if (authenticationException is not null)
            {
                throw authenticationException;
            }

            return Task.CompletedTask;
        }
    }
}
