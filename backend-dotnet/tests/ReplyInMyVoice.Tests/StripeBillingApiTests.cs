using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ReplyInMyVoice.Infrastructure.Data;
using ReplyInMyVoice.Infrastructure.Services;

namespace ReplyInMyVoice.Tests;

public sealed class StripeBillingApiTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    static StripeBillingApiTests()
    {
        Environment.SetEnvironmentVariable("DOTNET_USE_POLLING_FILE_WATCHER", "1");
        Environment.SetEnvironmentVariable("DOTNET_hostBuilder__reloadConfigOnChange", "false");
        Environment.SetEnvironmentVariable("ASPNETCORE_hostBuilder__reloadConfigOnChange", "false");
    }

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
    public async Task Checkout_requires_authentication()
    {
        await using var factory = CreateFactory(new FakeStripeBillingService("https://billing.test/checkout"));
        var client = CreateClient(factory);

        var response = await client.PostAsync("/api/stripe/checkout", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Checkout_returns_billing_url_for_authenticated_user()
    {
        var fakeBilling = new FakeStripeBillingService("https://billing.test/checkout");
        await using var factory = CreateFactory(fakeBilling);
        var client = CreateClient(factory);
        client.DefaultRequestHeaders.Add("X-External-User-Id", "clerk_checkout");

        var response = await client.PostAsync("/api/stripe/checkout", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<BillingUrlResponse>();
        body!.Url.Should().Be("https://billing.test/checkout");
        fakeBilling.CheckoutUserId.Should().Be("clerk_checkout");
        fakeBilling.CheckoutSku.Should().BeNull();
    }

    [Fact]
    public async Task Checkout_returns_5xx_and_persists_no_state_when_billing_service_fails()
    {
        var fakeBilling = new FakeStripeBillingService("https://billing.test/checkout")
        {
            CheckoutError = new TaskCanceledException("simulated Stripe timeout"),
        };
        await using var factory = CreateFactory(fakeBilling);
        var client = CreateClient(factory);
        client.DefaultRequestHeaders.Add("X-External-User-Id", "clerk_checkout_failure");
        client.DefaultRequestHeaders.Add("X-User-Email", "checkout-failure@example.com");

        var response = await client.PostAsJsonAsync("/api/stripe/checkout", new { sku = "quick_pack" });

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Billing provider request failed");
        fakeBilling.CheckoutUserId.Should().Be("clerk_checkout_failure");
        fakeBilling.CheckoutSku.Should().Be("quick_pack");

        await using var db = CreateContext();
        (await db.AppUsers.CountAsync()).Should().Be(0);
        (await db.RewriteCredits.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Checkout_forwards_allowed_sku_to_billing_service()
    {
        var fakeBilling = new FakeStripeBillingService("https://billing.test/checkout");
        await using var factory = CreateFactory(fakeBilling);
        var client = CreateClient(factory);
        client.DefaultRequestHeaders.Add("X-External-User-Id", "clerk_checkout_sku");

        var response = await client.PostAsJsonAsync("/api/stripe/checkout", new { sku = "quick_pack" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        fakeBilling.CheckoutUserId.Should().Be("clerk_checkout_sku");
        fakeBilling.CheckoutSku.Should().Be("quick_pack");
    }

    [Fact]
    public async Task Checkout_rejects_unknown_sku()
    {
        var fakeBilling = new FakeStripeBillingService("https://billing.test/checkout");
        await using var factory = CreateFactory(fakeBilling);
        var client = CreateClient(factory);
        client.DefaultRequestHeaders.Add("X-External-User-Id", "clerk_checkout_bad_sku");

        var response = await client.PostAsJsonAsync("/api/stripe/checkout", new { sku = "unknown_pack" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        fakeBilling.CheckoutUserId.Should().BeNull();
    }

    [Fact]
    public async Task Portal_maps_missing_stripe_customer_to_bad_request()
    {
        await using var factory = CreateFactory(new FakeStripeBillingService("https://billing.test/checkout")
        {
            PortalError = new InvalidOperationException("stripe_customer_missing")
        });
        var client = CreateClient(factory);
        client.DefaultRequestHeaders.Add("X-External-User-Id", "clerk_portal");

        var response = await client.PostAsync("/api/stripe/portal", null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private WebApplicationFactory<Program> CreateFactory(IStripeBillingService billingService)
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureServices(services =>
                {
                    var dbOptions = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
                    if (dbOptions is not null)
                    {
                        services.Remove(dbOptions);
                    }

                    var existingBilling = services.SingleOrDefault(d => d.ServiceType == typeof(IStripeBillingService));
                    if (existingBilling is not null)
                    {
                        services.Remove(existingBilling);
                    }

                    services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));
                    services.AddSingleton(billingService);
                });
            });
    }

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false,
        });

    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new AppDbContext(options);
    }
}

internal sealed class FakeStripeBillingService(string checkoutUrl) : IStripeBillingService
{
    public string? CheckoutUserId { get; private set; }
    public string? CheckoutSku { get; private set; }
    public Exception? CheckoutError { get; init; }
    public InvalidOperationException? PortalError { get; init; }

    public Task<string> CreateCheckoutSessionUrlAsync(
        string externalAuthUserId,
        string? email,
        string? sku,
        CancellationToken cancellationToken)
    {
        CheckoutUserId = externalAuthUserId;
        CheckoutSku = sku;
        if (CheckoutError is not null)
        {
            throw CheckoutError;
        }

        return Task.FromResult(checkoutUrl);
    }

    public Task<string> CreatePortalSessionUrlAsync(
        string externalAuthUserId,
        CancellationToken cancellationToken)
    {
        if (PortalError is not null)
        {
            throw PortalError;
        }

        return Task.FromResult("https://billing.test/portal");
    }
}

public sealed record BillingUrlResponse(string Url);
