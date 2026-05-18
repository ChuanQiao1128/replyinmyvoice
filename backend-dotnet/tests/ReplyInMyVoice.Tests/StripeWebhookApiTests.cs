using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Infrastructure.Data;

namespace ReplyInMyVoice.Tests;

public sealed class StripeWebhookApiTests : IAsyncLifetime
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
    public async Task Stripe_webhook_marks_duplicate_event_as_not_processed()
    {
        await using var factory = CreateFactory();
        var client = factory.CreateClient();
        var payload = JsonSerializer.Serialize(new { id = "evt_duplicate", type = "customer.subscription.updated" });

        var first = await client.PostAsync("/api/stripe/webhook", JsonContent(payload));
        var second = await client.PostAsync("/api/stripe/webhook", JsonContent(payload));

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        var secondBody = await second.Content.ReadAsStringAsync();
        secondBody.Should().Contain("\"processed\":false");

        await using var db = CreateContext();
        (await db.StripeEvents.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Stripe_webhook_updates_subscription_entitlement_from_subscription_event()
    {
        await using var db = CreateContext();
        db.AppUsers.Add(new AppUser
        {
            ExternalAuthUserId = "clerk_billing",
            Email = "billing@example.com",
            StripeCustomerId = "cus_entitlement",
            SubscriptionStatus = SubscriptionStatus.Inactive,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        await using var factory = CreateFactory();
        var client = factory.CreateClient();
        var payload = JsonSerializer.Serialize(new
        {
            id = "evt_subscription_active",
            type = "customer.subscription.updated",
            data = new
            {
                @object = new
                {
                    id = "sub_entitlement",
                    customer = "cus_entitlement",
                    status = "active",
                    current_period_end = 1770000000
                }
            }
        });

        var response = await client.PostAsync("/api/stripe/webhook", JsonContent(payload));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await using var verifyDb = CreateContext();
        var user = await verifyDb.AppUsers.SingleAsync();
        user.StripeSubscriptionId.Should().Be("sub_entitlement");
        user.SubscriptionStatus.Should().Be(SubscriptionStatus.Active);
        user.CurrentPeriodEnd.Should().NotBeNull();
    }

    [Fact]
    public async Task Stripe_webhook_rejects_missing_signature_in_production_when_secret_configured()
    {
        await using var factory = CreateFactory("Production");
        var client = factory.CreateClient();
        var payload = JsonSerializer.Serialize(new { id = "evt_missing_signature", type = "customer.subscription.updated" });

        var response = await client.PostAsync("/api/stripe/webhook", JsonContent(payload));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private WebApplicationFactory<Program> CreateFactory(string environment = "Testing")
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment(environment);
                builder.ConfigureServices(services =>
                {
                    var dbOptions = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
                    if (dbOptions is not null)
                    {
                        services.Remove(dbOptions);
                    }

                    services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));
                });
                builder.UseSetting("STRIPE_WEBHOOK_SECRET", "whsec_test_secret");
            });
    }

    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new AppDbContext(options);
    }

    private static StringContent JsonContent(string payload) =>
        new(payload, Encoding.UTF8, "application/json");
}
