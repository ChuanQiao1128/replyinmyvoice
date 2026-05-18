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
        var client = factory.CreateClient();

        var response = await client.PostAsync("/api/stripe/checkout", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Checkout_returns_billing_url_for_authenticated_user()
    {
        var fakeBilling = new FakeStripeBillingService("https://billing.test/checkout");
        await using var factory = CreateFactory(fakeBilling);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-External-User-Id", "clerk_checkout");

        var response = await client.PostAsync("/api/stripe/checkout", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<BillingUrlResponse>();
        body!.Url.Should().Be("https://billing.test/checkout");
        fakeBilling.CheckoutUserId.Should().Be("clerk_checkout");
    }

    [Fact]
    public async Task Portal_maps_missing_stripe_customer_to_bad_request()
    {
        await using var factory = CreateFactory(new FakeStripeBillingService("https://billing.test/checkout")
        {
            PortalError = new InvalidOperationException("stripe_customer_missing")
        });
        var client = factory.CreateClient();
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
    public InvalidOperationException? PortalError { get; init; }

    public Task<string> CreateCheckoutSessionUrlAsync(
        string externalAuthUserId,
        string? email,
        CancellationToken cancellationToken)
    {
        CheckoutUserId = externalAuthUserId;
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
