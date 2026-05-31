using System.Net;
using System.Net.Http.Json;
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

public sealed class AccountApiTests : IAsyncLifetime
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
    public async Task Me_upserts_authenticated_email_user_without_usage_side_effects()
    {
        await using var factory = CreateFactory();
        var client = CreateClient(factory);
        client.DefaultRequestHeaders.Add("X-External-User-Id", "entra_email_user");
        client.DefaultRequestHeaders.Add("X-User-Email", "teacher@example.com");

        var response = await client.GetAsync("/api/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AccountResponse>();
        body.Should().NotBeNull();
        body!.ExternalAuthUserId.Should().Be("entra_email_user");
        body.Email.Should().Be("teacher@example.com");

        await using var db = CreateContext();
        var user = await db.AppUsers.SingleAsync();
        user.ExternalAuthUserId.Should().Be("entra_email_user");
        user.Email.Should().Be("teacher@example.com");
        (await db.UsagePeriods.CountAsync()).Should().Be(0);
        (await db.RewriteAttempts.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Me_updates_email_for_existing_entra_subject_without_duplicate_user()
    {
        await using var factory = CreateFactory();
        var client = CreateClient(factory);
        client.DefaultRequestHeaders.Add("X-External-User-Id", "entra_same_subject");
        client.DefaultRequestHeaders.Add("X-User-Email", "first@example.com");
        (await client.GetAsync("/api/me")).StatusCode.Should().Be(HttpStatusCode.OK);

        client.DefaultRequestHeaders.Remove("X-User-Email");
        client.DefaultRequestHeaders.Add("X-User-Email", "updated@example.com");
        var response = await client.GetAsync("/api/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await using var db = CreateContext();
        var user = await db.AppUsers.SingleAsync();
        user.ExternalAuthUserId.Should().Be("entra_same_subject");
        user.Email.Should().Be("updated@example.com");
    }

    [Fact]
    public async Task Me_requires_authentication_and_does_not_create_user()
    {
        await using var factory = CreateFactory();
        var client = CreateClient(factory);

        var response = await client.GetAsync("/api/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await using var db = CreateContext();
        (await db.AppUsers.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Payments_returns_purchase_history_with_receipt_url()
    {
        var userId = Guid.NewGuid();
        await using (var db = CreateContext())
        {
            db.AppUsers.Add(new AppUser
            {
                Id = userId,
                ExternalAuthUserId = "entra_receipts",
                Email = "buyer@example.com",
                SubscriptionStatus = SubscriptionStatus.Inactive,
                CreatedAt = DateTimeOffset.Parse("2026-05-30T00:00:00Z"),
                UpdatedAt = DateTimeOffset.Parse("2026-05-30T00:00:00Z"),
            });
            db.RewriteCredits.Add(new RewriteCredit
            {
                UserId = userId,
                Source = "PURCHASE",
                AmountGranted = 10,
                AmountConsumed = 1,
                GrantedAt = DateTimeOffset.Parse("2026-05-30T10:00:00Z"),
                ExpiresAt = DateTimeOffset.Parse("2026-08-30T10:00:00Z"),
                StripeSku = "quick_pack",
                StripeAmountTotal = 250,
                StripeCurrency = "nzd",
                StripeReceiptUrl = "https://pay.stripe.test/receipts/quick-pack",
            });
            await db.SaveChangesAsync();
        }

        await using var factory = CreateFactory();
        var client = CreateClient(factory);
        client.DefaultRequestHeaders.Add("X-External-User-Id", "entra_receipts");
        client.DefaultRequestHeaders.Add("X-User-Email", "buyer@example.com");

        var response = await client.GetAsync("/api/me/payments");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<IReadOnlyList<AccountPaymentResponse>>();
        body.Should().NotBeNull();
        body.Should().ContainSingle();
        body![0].Sku.Should().Be("quick_pack");
        body[0].Amount.Should().Be(250);
        body[0].Currency.Should().Be("nzd");
        body[0].ReceiptUrl.Should().Be("https://pay.stripe.test/receipts/quick-pack");
        body[0].Remaining.Should().Be(9);
    }

    private WebApplicationFactory<Program> CreateFactory()
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

                    services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));
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

    private sealed record AccountResponse(
        Guid Id,
        string ExternalAuthUserId,
        string? Email,
        string SubscriptionStatus);

    private sealed record AccountPaymentResponse(
        string? Sku,
        long? Amount,
        string? Currency,
        DateTimeOffset Date,
        DateTimeOffset? Expiry,
        int Remaining,
        string? ReceiptUrl);
}
