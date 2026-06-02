using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReplyInMyVoice.Domain.Contracts;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Functions.Functions;
using ReplyInMyVoice.Infrastructure.Data;
using ReplyInMyVoice.Infrastructure.Queueing;

namespace ReplyInMyVoice.Tests;

public sealed class RewriteApiTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    static RewriteApiTests()
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
    public async Task Rewrite_returns_bad_request_when_idempotency_key_is_missing()
    {
        await using var factory = CreateFactory();
        var client = CreateClient(factory);
        client.DefaultRequestHeaders.Add("X-External-User-Id", "clerk_test");

        var response = await client.PostAsJsonAsync("/api/rewrite", ValidRequest());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Rewrite_validation_error_does_not_create_usage_or_attempt()
    {
        await using var factory = CreateFactory();
        var client = CreateClient(factory);
        client.DefaultRequestHeaders.Add("X-External-User-Id", "clerk_validation");
        client.DefaultRequestHeaders.Add("X-Idempotency-Key", "api-idem-validation");

        var response = await client.PostAsJsonAsync(
            "/api/rewrite",
            new RewriteRequest("message", "short", "client", "reply", "", "", "warm"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await using var db = CreateContext();
        (await db.RewriteAttempts.CountAsync()).Should().Be(0);
        (await db.UsagePeriods.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Rewrite_authentication_error_does_not_create_usage_or_attempt()
    {
        await using var factory = CreateFactory();
        var client = CreateClient(factory);
        client.DefaultRequestHeaders.Add("X-Idempotency-Key", "api-idem-unauthenticated");

        var response = await client.PostAsJsonAsync("/api/rewrite", ValidRequest());

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await using var db = CreateContext();
        (await db.RewriteAttempts.CountAsync()).Should().Be(0);
        (await db.UsagePeriods.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Rewrite_rejects_header_only_user_identity_in_production()
    {
        await using var factory = CreateFactory("Production");
        var client = CreateClient(factory);
        client.DefaultRequestHeaders.Add("X-External-User-Id", "clerk_spoofed");
        client.DefaultRequestHeaders.Add("X-Idempotency-Key", "api-idem-prod-header");

        var response = await client.PostAsJsonAsync("/api/rewrite", ValidRequest());

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Rewrite_without_promo_credit_returns_payment_required()
    {
        await using var factory = CreateFactory();
        var client = CreateClient(factory);
        client.DefaultRequestHeaders.Add("X-External-User-Id", "clerk_no_credit");
        client.DefaultRequestHeaders.Add("X-Idempotency-Key", "api-idem-no-credit");

        var response = await client.PostAsJsonAsync("/api/rewrite", ValidRequest());

        response.StatusCode.Should().Be(HttpStatusCode.PaymentRequired);
        await using var db = CreateContext();
        (await db.UsagePeriods.CountAsync()).Should().Be(0);
        (await db.RewriteAttempts.CountAsync()).Should().Be(0);
        (await db.UsageReservations.CountAsync()).Should().Be(0);
        (await db.OutboxMessages.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Rewrite_creates_attempt_and_outbox_message()
    {
        await SeedPromoCreditAsync("clerk_test", amountGranted: 3);
        await using var factory = CreateFactory();
        var client = CreateClient(factory);
        client.DefaultRequestHeaders.Add("X-External-User-Id", "clerk_test");
        client.DefaultRequestHeaders.Add("X-Idempotency-Key", "api-idem-1");

        var response = await client.PostAsJsonAsync("/api/rewrite", ValidRequest());

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var body = await response.Content.ReadFromJsonAsync<RewriteAttemptResponse>();
        body.Should().NotBeNull();
        body!.Status.Should().Be("Pending");

        await using var db = CreateContext();
        var outbox = await db.OutboxMessages.SingleAsync();
        outbox.Status.Should().Be(OutboxMessageStatus.Pending);
        outbox.MessageType.Should().Be("RewriteJobCreated");
        outbox.PayloadJson.Should().Contain(body.AttemptId.ToString());
    }

    [Fact]
    public async Task Rewrite_same_idempotency_key_with_different_body_returns_conflict_without_new_outbox()
    {
        await SeedPromoCreditAsync("clerk_conflict", amountGranted: 3);
        await using var factory = CreateFactory();
        var client = CreateClient(factory);
        client.DefaultRequestHeaders.Add("X-External-User-Id", "clerk_conflict");
        client.DefaultRequestHeaders.Add("X-Idempotency-Key", "api-idem-conflict");

        var first = await client.PostAsJsonAsync("/api/rewrite", ValidRequest());
        var second = await client.PostAsJsonAsync(
            "/api/rewrite",
            new RewriteRequest(
                "Can you send an update?",
                "This request body is different and should not reuse the same idempotency key.",
                "client",
                "reply",
                "The report is still being checked.",
                "No promised timeline.",
                "direct"));

        first.StatusCode.Should().Be(HttpStatusCode.Accepted);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await using var db = CreateContext();
        (await db.RewriteAttempts.CountAsync()).Should().Be(1);
        (await db.UsageReservations.CountAsync()).Should().Be(1);
        (await db.OutboxMessages.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Attempt_lookup_returns_attempt_status_for_same_user()
    {
        await SeedPromoCreditAsync("clerk_test", amountGranted: 3);
        await using var factory = CreateFactory();
        var client = CreateClient(factory);
        client.DefaultRequestHeaders.Add("X-External-User-Id", "clerk_test");
        client.DefaultRequestHeaders.Add("X-Idempotency-Key", "api-idem-lookup");
        var createResponse = await client.PostAsJsonAsync("/api/rewrite", ValidRequest());
        var created = await createResponse.Content.ReadFromJsonAsync<RewriteAttemptResponse>();

        var lookup = await client.GetAsync($"/api/rewrite-attempts/{created!.AttemptId}");

        lookup.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await lookup.Content.ReadFromJsonAsync<RewriteAttemptResponse>();
        body!.AttemptId.Should().Be(created.AttemptId);
        body.Status.Should().Be("Pending");
    }

    [Fact]
    public async Task Rewrite_retry_after_success_returns_same_result_without_new_reservation()
    {
        await SeedPromoCreditAsync("clerk_retry_success", amountGranted: 3);
        await using var factory = CreateFactory();
        var client = CreateClient(factory);
        client.DefaultRequestHeaders.Add("X-External-User-Id", "clerk_retry_success");
        client.DefaultRequestHeaders.Add("X-Idempotency-Key", "api-idem-retry-success");
        var createResponse = await client.PostAsJsonAsync("/api/rewrite", ValidRequest());
        var created = await createResponse.Content.ReadFromJsonAsync<RewriteAttemptResponse>();
        var resultJson = "{\"rewrittenText\":\"Done\",\"changeSummary\":[],\"riskNotes\":[]}";
        await using (var db = CreateContext())
        {
            var quota = new ReplyInMyVoice.Infrastructure.Services.QuotaService(() => CreateContext());
            await quota.FinalizeSuccessAsync(created!.AttemptId, resultJson, DateTimeOffset.UtcNow);
        }

        var retry = await client.PostAsJsonAsync("/api/rewrite", ValidRequest());

        retry.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await retry.Content.ReadFromJsonAsync<RewriteAttemptResponse>();
        body!.AttemptId.Should().Be(created!.AttemptId);
        body.ResultJson.Should().Be(resultJson);
        await using var verifyDb = CreateContext();
        (await verifyDb.RewriteAttempts.CountAsync()).Should().Be(1);
        (await verifyDb.UsageReservations.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Rewrite_paid_quota_is_ninety_requests_per_period()
    {
        await using (var db = CreateContext())
        {
            db.AppUsers.Add(new AppUser
            {
                ExternalAuthUserId = "clerk_paid_quota",
                Email = "paid@example.com",
                StripeCustomerId = "cus_paid_quota",
                StripeSubscriptionId = "sub_paid_quota",
                SubscriptionStatus = SubscriptionStatus.Active,
                CurrentPeriodEnd = DateTimeOffset.Parse("2026-06-20T00:00:00Z"),
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        await using var factory = CreateFactory();
        var client = CreateClient(factory);
        client.DefaultRequestHeaders.Add("X-External-User-Id", "clerk_paid_quota");

        for (var index = 0; index < 90; index += 1)
        {
            var accepted = await PostRewriteAsync(client, $"api-idem-paid-{index}");
            accepted.StatusCode.Should().Be(HttpStatusCode.Accepted);
        }

        var exhausted = await PostRewriteAsync(client, "api-idem-paid-90");

        exhausted.StatusCode.Should().Be(HttpStatusCode.PaymentRequired);
        await using var verifyDb = CreateContext();
        var period = await verifyDb.UsagePeriods.SingleAsync();
        period.QuotaLimit.Should().Be(90);
        period.ReservedCount.Should().Be(90);
        (await verifyDb.RewriteAttempts.CountAsync()).Should().Be(90);
    }

    [Fact]
    public async Task Ready_health_returns_service_unavailable_when_dependency_or_backlog_check_fails()
    {
        var now = DateTimeOffset.UtcNow;
        await using (var db = CreateContext())
        {
            db.StripeEvents.Add(new StripeEvent
            {
                EventId = "evt_ready_failed",
                Type = "checkout.session.completed",
                Status = StripeEventStatus.Failed,
                CreatedAt = now.AddMinutes(-5),
                LastAttemptAt = now.AddMinutes(-5),
                LastError = "temporary failure",
            });
            db.OutboxMessages.Add(new OutboxMessage
            {
                MessageType = "RewriteJobCreated",
                PayloadJson = "{}",
                Status = OutboxMessageStatus.Pending,
                CreatedAt = now.AddMinutes(-11),
                NextAttemptAt = now.AddMinutes(-11),
            });

            var user = new AppUser
            {
                ExternalAuthUserId = "clerk_ready_stuck",
                Email = "ready@example.com",
                CreatedAt = now,
                UpdatedAt = now,
            };
            var period = new UsagePeriod
            {
                User = user,
                PeriodKey = "ready-test",
                QuotaLimit = 3,
                ReservedCount = 1,
                CreatedAt = now,
                UpdatedAt = now,
            };
            var attempt = new RewriteAttempt
            {
                User = user,
                IdempotencyKey = "ready-idem",
                RequestHash = "ready-hash",
                RequestJson = "{}",
                Status = RewriteAttemptStatus.Processing,
                CreatedAt = now.AddMinutes(-20),
                ExpiresAt = now.AddMinutes(-1),
            };
            db.UsageReservations.Add(new UsageReservation
            {
                User = user,
                UsagePeriod = period,
                RewriteAttempt = attempt,
                Status = UsageReservationStatus.Pending,
                CreatedAt = now.AddMinutes(-20),
                ExpiresAt = now.AddMinutes(-1),
            });
            await db.SaveChangesAsync();
        }

        await using var healthDb = CreateContext();
        var function = new HealthFunction(healthDb, BuildHealthConfiguration());

        var result = await function.ReadinessHealth(
            new DefaultHttpContext().Request,
            CancellationToken.None);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
        var json = JsonSerializer.Serialize(objectResult.Value);
        json.Should().Contain("\"database\"");
        json.Should().Contain("\"serviceBus\"");
        json.Should().Contain("\"failedStripeEvents\"");
        json.Should().Contain("\"outboxBacklog\"");
        json.Should().Contain("\"stuckReservations\"");
        json.Should().Contain("\"ok\":false");
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
                    services.AddSingleton<InMemoryRewriteJobPublisher>();
                    services.AddSingleton<IRewriteJobPublisher>(sp => sp.GetRequiredService<InMemoryRewriteJobPublisher>());
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

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false,
        });

    private static IConfiguration BuildHealthConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Health:OutboxBacklogMinutes"] = "10",
            })
            .Build();

    private async Task SeedPromoCreditAsync(string externalAuthUserId, int amountGranted)
    {
        var now = DateTimeOffset.UtcNow;
        await using var db = CreateContext();
        var user = new AppUser
        {
            ExternalAuthUserId = externalAuthUserId,
            Email = $"{externalAuthUserId}@example.com",
            SubscriptionStatus = SubscriptionStatus.Inactive,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.AppUsers.Add(user);
        db.RewriteCredits.Add(new RewriteCredit
        {
            User = user,
            Source = "PROMO",
            AmountGranted = amountGranted,
            AmountConsumed = 0,
            GrantedAt = now,
            ExpiresAt = now.AddDays(90),
        });
        await db.SaveChangesAsync();
    }

    private static Task<HttpResponseMessage> PostRewriteAsync(HttpClient client, string idempotencyKey)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/rewrite")
        {
            Content = JsonContent.Create(ValidRequest()),
        };
        request.Headers.Add("X-Idempotency-Key", idempotencyKey);
        return client.SendAsync(request);
    }

    private static RewriteRequest ValidRequest() =>
        new(
            "Can you send an update?",
            "Thank you for your email. I will send an update soon.",
            "client",
            "reply",
            "The report is still being checked.",
            "No promised timeline.",
            "warm");
}

public sealed record RewriteAttemptResponse(
    Guid AttemptId,
    string Status,
    string? ResultJson,
    string? ErrorCode);
