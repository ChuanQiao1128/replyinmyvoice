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
using ReplyInMyVoice.Infrastructure.Services;

namespace ReplyInMyVoice.Tests;

public sealed class RewriteApiTests : IAsyncLifetime
{
    private const string TestApiKeyPepper = "rewrite-api-v1-test-pepper";
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
    public async Task V1_rewrite_submit_with_valid_key_reserves_usage_and_returns_processing_id()
    {
        var (user, token) = await SeedApiKeyUserAsync(
            "clerk_v1_submit",
            SubscriptionStatus.Active,
            currentPeriodEnd: DateTimeOffset.Parse("2026-07-01T00:00:00Z"));
        await using var factory = CreateFactory();
        var client = CreateClient(factory);

        var response = await PostV1RewriteAsync(client, token, "v1-submit-success", ValidV1Draft());

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var body = await response.Content.ReadFromJsonAsync<V1RewriteSubmitResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().NotBeEmpty();
        body.Status.Should().Be("processing");
        response.Headers.Location?.ToString().Should().Be($"/api/v1/rewrite/{body.Id}");

        await using var db = CreateContext();
        var reservation = await db.UsageReservations.SingleAsync();
        reservation.UserId.Should().Be(user.Id);
        reservation.RewriteAttemptId.Should().Be(body.Id);
        reservation.Status.Should().Be(UsageReservationStatus.Pending);
        var period = await db.UsagePeriods.SingleAsync();
        period.UserId.Should().Be(user.Id);
        period.UsedCount.Should().Be(0);
        period.ReservedCount.Should().Be(1);
        var usage = await db.ApiKeyUsages.SingleAsync();
        usage.Endpoint.Should().Be("v1/rewrite");
        usage.StatusCode.Should().Be(StatusCodes.Status202Accepted);
        usage.RequestId.Should().Be(body.Id.ToString());
        usage.LatencyMs.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task V1_rewrite_submit_enforces_per_key_rate_limit_without_reservation_for_rejected_call()
    {
        const int rateLimitPerMinute = 3;
        var (user, token) = await SeedApiKeyUserAsync(
            "clerk_v1_rate_limit",
            SubscriptionStatus.Active,
            currentPeriodEnd: DateTimeOffset.Parse("2026-07-01T00:00:00Z"),
            rateLimitPerMinute);
        await using var factory = CreateFactory();
        var client = CreateClient(factory);

        for (var index = 0; index < rateLimitPerMinute; index += 1)
        {
            var accepted = await PostV1RewriteAsync(
                client,
                token,
                $"v1-rate-limit-{index}",
                ValidV1Draft());

            accepted.StatusCode.Should().Be(HttpStatusCode.Accepted);
        }

        var rejected = await PostV1RewriteAsync(
            client,
            token,
            "v1-rate-limit-rejected",
            ValidV1Draft());

        rejected.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        await AssertErrorCodeAsync(rejected, "rate_limited");

        await using var db = CreateContext();
        (await db.RewriteAttempts.CountAsync()).Should().Be(rateLimitPerMinute);
        (await db.UsageReservations.CountAsync()).Should().Be(rateLimitPerMinute);
        (await db.OutboxMessages.CountAsync()).Should().Be(rateLimitPerMinute);
        var period = await db.UsagePeriods.SingleAsync();
        period.UserId.Should().Be(user.Id);
        period.UsedCount.Should().Be(0);
        period.ReservedCount.Should().Be(rateLimitPerMinute);
        var usageStatusCodes = await db.ApiKeyUsages
            .Select(x => x.StatusCode)
            .ToListAsync();
        usageStatusCodes.Should().HaveCount(rateLimitPerMinute + 1);
        usageStatusCodes.Count(x => x == StatusCodes.Status202Accepted).Should().Be(rateLimitPerMinute);
        usageStatusCodes.Count(x => x == StatusCodes.Status429TooManyRequests).Should().Be(1);
    }

    [Fact]
    public async Task V1_rewrite_submit_rejects_over_word_or_character_cap_without_reservation()
    {
        var (_, token) = await SeedApiKeyUserAsync(
            "clerk_v1_too_long",
            SubscriptionStatus.Active,
            currentPeriodEnd: DateTimeOffset.Parse("2026-07-01T00:00:00Z"));
        await using var factory = CreateFactory();
        var client = CreateClient(factory);
        var wordHeavyDraft = string.Join(" ", Enumerable.Repeat("word", 301));
        var characterHeavyDraft = new string('a', 2401);

        var wordResponse = await PostV1RewriteAsync(client, token, "v1-too-many-words", wordHeavyDraft);
        var characterResponse = await PostV1RewriteAsync(client, token, "v1-too-many-characters", characterHeavyDraft);

        wordResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertErrorCodeAsync(wordResponse, "input_too_long");
        characterResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertErrorCodeAsync(characterResponse, "input_too_long");
        await using var db = CreateContext();
        (await db.RewriteAttempts.CountAsync()).Should().Be(0);
        (await db.UsageReservations.CountAsync()).Should().Be(0);
        (await db.OutboxMessages.CountAsync()).Should().Be(0);
        (await db.ApiKeyUsages.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task V1_rewrite_submit_rejects_missing_or_invalid_key_without_reservation()
    {
        await using var factory = CreateFactory();
        var client = CreateClient(factory);

        var missing = await client.PostAsJsonAsync("/api/v1/rewrite", new { draft = ValidV1Draft() });
        var invalid = await PostV1RewriteAsync(client, "rmv_live_unknown_api_key", "v1-invalid-key", ValidV1Draft());

        missing.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await AssertErrorCodeAsync(missing, "invalid_key");
        invalid.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await AssertErrorCodeAsync(invalid, "invalid_key");
        await using var db = CreateContext();
        (await db.RewriteAttempts.CountAsync()).Should().Be(0);
        (await db.UsageReservations.CountAsync()).Should().Be(0);
        (await db.ApiKeyUsages.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task V1_rewrite_submit_returns_quota_exhausted_without_reservation_when_no_quota_or_credit()
    {
        var (_, token) = await SeedApiKeyUserAsync(
            "clerk_v1_no_quota",
            SubscriptionStatus.Inactive,
            currentPeriodEnd: null);
        await using var factory = CreateFactory();
        var client = CreateClient(factory);

        var response = await PostV1RewriteAsync(client, token, "v1-no-quota", ValidV1Draft());

        response.StatusCode.Should().Be(HttpStatusCode.PaymentRequired);
        await AssertErrorCodeAsync(response, "quota_exhausted");
        await using var db = CreateContext();
        (await db.RewriteAttempts.CountAsync()).Should().Be(0);
        (await db.UsageReservations.CountAsync()).Should().Be(0);
        (await db.OutboxMessages.CountAsync()).Should().Be(0);
        (await db.UsagePeriods.CountAsync()).Should().Be(0);
        var usage = await db.ApiKeyUsages.SingleAsync();
        usage.Endpoint.Should().Be("v1/rewrite");
        usage.StatusCode.Should().Be(StatusCodes.Status402PaymentRequired);
    }

    [Fact]
    public async Task V1_rewrite_result_maps_pending_succeeded_and_failed_attempts()
    {
        var (user, token) = await SeedApiKeyUserAsync(
            "clerk_v1_result_states",
            SubscriptionStatus.Active,
            currentPeriodEnd: DateTimeOffset.Parse("2026-07-01T00:00:00Z"));
        var pending = await SeedV1AttemptAsync(user.Id, RewriteAttemptStatus.Pending);
        var succeeded = await SeedV1AttemptAsync(
            user.Id,
            RewriteAttemptStatus.Succeeded,
            resultJson: """
                {
                  "rewrittenText": "Hi Sam - the report is still being checked, and I will send a clear update soon.",
                  "naturalness": {
                    "draftAiLikePercent": 78,
                    "rewriteAiLikePercent": 24
                  }
                }
                """);
        var failed = await SeedV1AttemptAsync(
            user.Id,
            RewriteAttemptStatus.Failed,
            errorCode: "provider_timeout");
        await using var factory = CreateFactory();
        var client = CreateClient(factory);

        var pendingResponse = await GetV1RewriteResultAsync(client, token, pending.Id);
        var succeededResponse = await GetV1RewriteResultAsync(client, token, succeeded.Id);
        var failedResponse = await GetV1RewriteResultAsync(client, token, failed.Id);

        pendingResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        using (var pendingJson = await ReadJsonAsync(pendingResponse))
        {
            pendingJson.RootElement.GetProperty("id").GetGuid().Should().Be(pending.Id);
            pendingJson.RootElement.GetProperty("status").GetString().Should().Be("processing");
        }

        succeededResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        using (var succeededJson = await ReadJsonAsync(succeededResponse))
        {
            succeededJson.RootElement.GetProperty("id").GetGuid().Should().Be(succeeded.Id);
            succeededJson.RootElement.GetProperty("status").GetString().Should().Be("succeeded");
            succeededJson.RootElement.GetProperty("rewrittenText").GetString().Should().Be(
                "Hi Sam - the report is still being checked, and I will send a clear update soon.");
            var signal = succeededJson.RootElement.GetProperty("signal");
            signal.GetProperty("draft").GetInt32().Should().Be(78);
            signal.GetProperty("rewrite").GetInt32().Should().Be(24);
        }

        failedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        using (var failedJson = await ReadJsonAsync(failedResponse))
        {
            failedJson.RootElement.GetProperty("id").GetGuid().Should().Be(failed.Id);
            failedJson.RootElement.GetProperty("status").GetString().Should().Be("failed");
            failedJson.RootElement.GetProperty("error").GetProperty("code").GetString().Should().Be("provider_timeout");
        }
    }

    [Fact]
    public async Task V1_rewrite_result_returns_not_found_for_attempt_owned_by_another_user()
    {
        var (_, token) = await SeedApiKeyUserAsync(
            "clerk_v1_result_owner",
            SubscriptionStatus.Active,
            currentPeriodEnd: DateTimeOffset.Parse("2026-07-01T00:00:00Z"));
        var (otherUser, _) = await SeedApiKeyUserAsync(
            "clerk_v1_result_other",
            SubscriptionStatus.Active,
            currentPeriodEnd: DateTimeOffset.Parse("2026-07-01T00:00:00Z"));
        var otherAttempt = await SeedV1AttemptAsync(otherUser.Id, RewriteAttemptStatus.Succeeded);
        await using var factory = CreateFactory();
        var client = CreateClient(factory);

        var response = await GetV1RewriteResultAsync(client, token, otherAttempt.Id);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await AssertErrorCodeAsync(response, "not_found");
    }

    [Fact]
    public async Task V1_rewrite_result_writes_api_key_usage_row()
    {
        var (user, token) = await SeedApiKeyUserAsync(
            "clerk_v1_result_usage",
            SubscriptionStatus.Active,
            currentPeriodEnd: DateTimeOffset.Parse("2026-07-01T00:00:00Z"));
        var attempt = await SeedV1AttemptAsync(user.Id, RewriteAttemptStatus.Pending);
        await using var factory = CreateFactory();
        var client = CreateClient(factory);

        var response = await GetV1RewriteResultAsync(client, token, attempt.Id);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await using var db = CreateContext();
        var usage = await db.ApiKeyUsages.SingleAsync();
        usage.Endpoint.Should().Be("v1/rewrite/{id}");
        usage.StatusCode.Should().Be(StatusCodes.Status200OK);
        usage.RequestId.Should().Be(attempt.Id.ToString());
        usage.LatencyMs.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task V1_rewrite_result_rejects_missing_or_invalid_key()
    {
        var (user, _) = await SeedApiKeyUserAsync(
            "clerk_v1_result_auth",
            SubscriptionStatus.Active,
            currentPeriodEnd: DateTimeOffset.Parse("2026-07-01T00:00:00Z"));
        var attempt = await SeedV1AttemptAsync(user.Id, RewriteAttemptStatus.Pending);
        await using var factory = CreateFactory();
        var client = CreateClient(factory);

        var missing = await client.GetAsync($"/api/v1/rewrite/{attempt.Id}");
        var invalid = await GetV1RewriteResultAsync(client, "rmv_live_unknown_api_key", attempt.Id);

        missing.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await AssertErrorCodeAsync(missing, "invalid_key");
        invalid.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await AssertErrorCodeAsync(invalid, "invalid_key");
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
                CreatedAt = now.AddHours(-2),
                LastAttemptAt = now.AddHours(-2),
                LastError = "temporary failure",
            });
            db.StripeEvents.Add(new StripeEvent
            {
                EventId = "evt_ready_processed",
                Type = "checkout.session.completed",
                Status = StripeEventStatus.Processed,
                CreatedAt = now.AddMinutes(-4),
                LastAttemptAt = now.AddMinutes(-4),
                ProcessedAt = now.AddMinutes(-4),
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
        json.Should().Contain("\"lastProcessedStripeEvent\"");
        json.Should().Contain("\"outboxBacklog\"");
        json.Should().Contain("\"stuckReservations\"");
        json.Should().Contain("\"ok\":false");

        using var document = JsonDocument.Parse(json);
        var checks = document.RootElement.GetProperty("checks");
        var failedStripeEvents = checks.GetProperty("failedStripeEvents");
        failedStripeEvents.GetProperty("count").GetInt32().Should().Be(1);
        failedStripeEvents.GetProperty("ok").GetBoolean().Should().BeFalse();

        var lastProcessed = checks.GetProperty("lastProcessedStripeEvent");
        lastProcessed.GetProperty("ok").GetBoolean().Should().BeTrue();
        lastProcessed.GetProperty("lastProcessedAt").GetDateTimeOffset().Should().BeCloseTo(
            now.AddMinutes(-4),
            TimeSpan.FromSeconds(5));
        lastProcessed.GetProperty("ageSeconds").GetInt64().Should().BeInRange(240, 300);
        lastProcessed.GetProperty("maxAgeMinutes").GetInt32().Should().Be(60);
    }

    [Fact]
    public async Task Ready_health_reports_no_processed_stripe_events_when_age_threshold_is_configured()
    {
        await using var healthDb = CreateContext();
        var function = new HealthFunction(healthDb, BuildHealthConfiguration());

        var result = await function.ReadinessHealth(
            new DefaultHttpContext().Request,
            CancellationToken.None);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
        var json = JsonSerializer.Serialize(objectResult.Value);
        using var document = JsonDocument.Parse(json);
        var lastProcessed = document.RootElement
            .GetProperty("checks")
            .GetProperty("lastProcessedStripeEvent");
        lastProcessed.GetProperty("ok").GetBoolean().Should().BeFalse();
        lastProcessed.GetProperty("error").GetString().Should().Be("no_processed_events");
        lastProcessed.GetProperty("ageSeconds").ValueKind.Should().Be(JsonValueKind.Null);
        lastProcessed.GetProperty("maxAgeMinutes").GetInt32().Should().Be(60);
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
                ["Health:StripeLastProcessedMaxAgeMinutes"] = "60",
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

    private async Task<(AppUser User, string Token)> SeedApiKeyUserAsync(
        string externalAuthUserId,
        SubscriptionStatus subscriptionStatus,
        DateTimeOffset? currentPeriodEnd,
        int rateLimitPerMinute = 60)
    {
        Environment.SetEnvironmentVariable("API_KEY_PEPPER", TestApiKeyPepper);
        var now = DateTimeOffset.UtcNow;
        var token = $"rmv_live_{externalAuthUserId}_token";
        await using var db = CreateContext();
        var user = new AppUser
        {
            ExternalAuthUserId = externalAuthUserId,
            Email = $"{externalAuthUserId}@example.com",
            StripeCustomerId = subscriptionStatus == SubscriptionStatus.Inactive ? null : $"cus_{externalAuthUserId}",
            StripeSubscriptionId = subscriptionStatus == SubscriptionStatus.Inactive ? null : $"sub_{externalAuthUserId}",
            SubscriptionStatus = subscriptionStatus,
            CurrentPeriodEnd = currentPeriodEnd,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.AppUsers.Add(user);
        db.ApiKeys.Add(new ApiKey
        {
            User = user,
            Name = "V1 integration key",
            KeyHash = ApiKeyService.ComputeHash(token),
            Last4 = token[^4..],
            RateLimitPerMinute = rateLimitPerMinute,
            CreatedAt = now,
            UpdatedAt = now,
        });
        await db.SaveChangesAsync();
        return (user, token);
    }

    private async Task<RewriteAttempt> SeedV1AttemptAsync(
        Guid userId,
        RewriteAttemptStatus status,
        string? resultJson = null,
        string? errorCode = null)
    {
        var now = DateTimeOffset.UtcNow;
        await using var db = CreateContext();
        var attempt = new RewriteAttempt
        {
            UserId = userId,
            IdempotencyKey = $"v1-result-{Guid.NewGuid():N}",
            RequestHash = Guid.NewGuid().ToString("N"),
            RequestJson = "{\"roughDraftReply\":\"Please send a clear update.\"}",
            Status = status,
            ResultJson = resultJson,
            ErrorCode = errorCode,
            CreatedAt = now,
            CompletedAt = status is RewriteAttemptStatus.Succeeded or RewriteAttemptStatus.Failed or RewriteAttemptStatus.Expired
                ? now
                : null,
            ExpiresAt = now.AddMinutes(15),
        };
        db.RewriteAttempts.Add(attempt);
        await db.SaveChangesAsync();
        return attempt;
    }

    private static Task<HttpResponseMessage> PostV1RewriteAsync(
        HttpClient client,
        string token,
        string idempotencyKey,
        string draft)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/rewrite")
        {
            Content = JsonContent.Create(new { draft }),
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        return client.SendAsync(request);
    }

    private static Task<HttpResponseMessage> GetV1RewriteResultAsync(
        HttpClient client,
        string token,
        Guid id)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/rewrite/{id}");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client.SendAsync(request);
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(json);
    }

    private static async Task AssertErrorCodeAsync(HttpResponseMessage response, string expectedCode)
    {
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        document.RootElement
            .GetProperty("error")
            .GetProperty("code")
            .GetString()
            .Should()
            .Be(expectedCode);
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

    private static string ValidV1Draft() =>
        "Please let the client know the report is still being checked and I will send a clear update soon.";
}

public sealed record RewriteAttemptResponse(
    Guid AttemptId,
    string Status,
    string? ResultJson,
    string? ErrorCode);

public sealed record V1RewriteSubmitResponse(
    Guid Id,
    string Status);
