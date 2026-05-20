using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ReplyInMyVoice.Domain.Contracts;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Infrastructure.Data;
using ReplyInMyVoice.Infrastructure.Queueing;

namespace ReplyInMyVoice.Tests;

public sealed class RewriteApiTests : IAsyncLifetime
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
    public async Task Rewrite_returns_bad_request_when_idempotency_key_is_missing()
    {
        await using var factory = CreateFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-External-User-Id", "clerk_test");

        var response = await client.PostAsJsonAsync("/api/rewrite", ValidRequest());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Rewrite_validation_error_does_not_create_usage_or_attempt()
    {
        await using var factory = CreateFactory();
        var client = factory.CreateClient();
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
        var client = factory.CreateClient();
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
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-External-User-Id", "clerk_spoofed");
        client.DefaultRequestHeaders.Add("X-Idempotency-Key", "api-idem-prod-header");

        var response = await client.PostAsJsonAsync("/api/rewrite", ValidRequest());

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Rewrite_creates_attempt_and_outbox_message()
    {
        await using var factory = CreateFactory();
        var client = factory.CreateClient();
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
        await using var factory = CreateFactory();
        var client = factory.CreateClient();
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
        await using var factory = CreateFactory();
        var client = factory.CreateClient();
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
        await using var factory = CreateFactory();
        var client = factory.CreateClient();
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
