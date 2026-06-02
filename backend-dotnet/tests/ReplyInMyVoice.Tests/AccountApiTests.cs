using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Infrastructure.Data;

namespace ReplyInMyVoice.Tests;

public sealed class AccountApiTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    static AccountApiTests()
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
    public async Task Me_includes_promo_block_and_trial_credit_label()
    {
        var userId = Guid.NewGuid();
        var promoCodeId = Guid.NewGuid();
        var creditId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.AddDays(45);

        await using (var db = CreateContext())
        {
            db.AppUsers.Add(new AppUser
            {
                Id = userId,
                ExternalAuthUserId = "entra_promo_api",
                Email = "promo-api@example.com",
                SubscriptionStatus = SubscriptionStatus.Inactive,
                CreatedAt = now,
                UpdatedAt = now,
            });
            db.PromoCodes.Add(new PromoCode
            {
                Id = promoCodeId,
                Code = "APICHECK",
                DisplayCode = "ApiCheck",
                Description = "Trial credits",
                Kind = PromoCodeKind.TrialCredits,
                CreditsGranted = 3,
                GrantTtlDays = 90,
                ValidFrom = now.AddDays(-1),
                ValidUntil = now.AddDays(30),
                MaxRedemptionsGlobal = 100,
                MaxRedemptionsPerUser = 1,
                RedemptionCount = 1,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
            });
            db.RewriteCredits.Add(new RewriteCredit
            {
                Id = creditId,
                UserId = userId,
                Source = "PROMO",
                AmountGranted = 3,
                AmountConsumed = 1,
                GrantedAt = now,
                ExpiresAt = expiresAt,
            });
            db.PromoCodeRedemptions.Add(new PromoCodeRedemption
            {
                PromoCodeId = promoCodeId,
                UserId = userId,
                RewriteCreditId = creditId,
                CreditsGranted = 3,
                CodeSnapshot = "APICHECK",
                Status = PromoCodeRedemptionStatus.Applied,
                RedeemedAt = now,
            });
            await db.SaveChangesAsync();
        }

        await using var factory = CreateFactory();
        var client = CreateClient(factory);
        client.DefaultRequestHeaders.Add("X-External-User-Id", "entra_promo_api");
        client.DefaultRequestHeaders.Add("X-User-Email", "promo-api@example.com");

        var response = await client.GetAsync("/api/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AccountResponse>();
        body.Should().NotBeNull();
        body!.Promo.Should().NotBeNull();
        body.Promo.HasRedeemed.Should().BeTrue();
        body.Promo.Eligible.Should().BeFalse();
        body.Promo.TrialRemaining.Should().Be(2);
        body.Promo.TrialExpiresAt.Should().BeCloseTo(expiresAt, TimeSpan.FromSeconds(1));
        body.Usage.Remaining.Should().Be(5);
        body.Usage.Sources.Should().ContainSingle(x =>
            x.Source == "PROMO" &&
            x.Label == "Trial rewrites" &&
            x.Remaining == 2);
    }

    private WebApplicationFactory<Program> CreateFactory()
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureLogging(logging => logging.ClearProviders());
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
        string SubscriptionStatus,
        AccountUsageResponse Usage,
        AccountPromoResponse Promo);

    private sealed record AccountUsageResponse(
        int Remaining,
        IReadOnlyList<AccountUsageSourceResponse> Sources);

    private sealed record AccountUsageSourceResponse(
        string Source,
        string Label,
        int Remaining);

    private sealed record AccountPromoResponse(
        bool HasRedeemed,
        bool Eligible,
        int TrialRemaining,
        DateTimeOffset? TrialExpiresAt);
}
