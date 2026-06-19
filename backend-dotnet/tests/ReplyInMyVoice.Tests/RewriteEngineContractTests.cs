using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.Common;
using ReplyInMyVoice.Application.UseCases.Account;
using ReplyInMyVoice.Application.UseCases.ApiKey;
using ReplyInMyVoice.Application.UseCases.Quota;
using ReplyInMyVoice.Application.UseCases.Rewrite;
using ReplyInMyVoice.Application.UseCases.RewriteJob;
using ReplyInMyVoice.Application.UseCases.WebhookOutbox;
using ReplyInMyVoice.Domain.Contracts;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Functions.Auth;
using ReplyInMyVoice.Functions.Functions;
using ReplyInMyVoice.Infrastructure.Data;
using ReplyInMyVoice.Infrastructure.Providers;
using ReplyInMyVoice.Infrastructure.Repositories;
using ReplyInMyVoice.Infrastructure.Services;
using AppWebhookDeliverySender = ReplyInMyVoice.Application.Abstractions.IWebhookDeliverySender;
using AppWebhookSendRequest = ReplyInMyVoice.Application.Abstractions.WebhookSendRequest;
using AppWebhookSendResult = ReplyInMyVoice.Application.Abstractions.WebhookSendResult;

namespace ReplyInMyVoice.Tests;

[Collection("ApiKeyPepper")]
public sealed class RewriteEngineContractTests
{
    private const string TestApiKeyPepper = "rewrite-engine-contract-test-pepper";
    private const string MinimalSuccessJson = "{\"rewrittenText\":\"Hi Sam, the report is ready.\",\"changeSummary\":[],\"riskNotes\":[],\"naturalness\":{\"draftAiLikePercent\":78,\"rewriteAiLikePercent\":24}}";
    private const string SuccessWithoutNaturalnessJson = "{\"rewrittenText\":\"Hi Sam, the report is ready.\",\"changeSummary\":[],\"riskNotes\":[]}";

    public static TheoryData<string> InvalidSuccessJsonCases => new()
    {
        "{\"changeSummary\":[],\"riskNotes\":[],\"naturalness\":{\"draftAiLikePercent\":78,\"rewriteAiLikePercent\":24}}",
        "{\"rewrittenText\":\"   \",\"changeSummary\":[],\"riskNotes\":[],\"naturalness\":{\"draftAiLikePercent\":78,\"rewriteAiLikePercent\":24}}",
        "{\"rewrittenText\":\"Hi Sam, the report is ready.\",\"riskNotes\":[],\"naturalness\":{\"draftAiLikePercent\":78,\"rewriteAiLikePercent\":24}}",
        "{\"rewrittenText\":\"Hi Sam, the report is ready.\",\"changeSummary\":[],\"naturalness\":{\"draftAiLikePercent\":78,\"rewriteAiLikePercent\":24}}",
    };

    [Fact]
    public async Task EngineContract_minimal_success_result_json_finalizes_attempt_and_persists_verbatim()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var now = DateTimeOffset.UtcNow;
        var attemptId = await ReserveAttemptAsync(fixture, user.Id, "engine-contract-success", now);
        var engine = new FakeRewriteEngineClient(new RewriteEngineResult(
            MinimalSuccessJson,
            Success: true,
            ErrorCode: null,
            [Metric(success: true)]));
        var costLogger = new FakeRewriteCostLogger();

        await using var handlerDb = fixture.CreateContext();
        await CreateProcessHandler(handlerDb, engine, costLogger)
            .HandleAsync(new ProcessRewriteJobCommand(attemptId));

        await using var verifyDb = fixture.CreateContext();
        var period = await verifyDb.UsagePeriods.SingleAsync();
        period.UsedCount.Should().Be(1);
        period.ReservedCount.Should().Be(0);

        var reservation = await verifyDb.UsageReservations.SingleAsync();
        reservation.Status.Should().Be(UsageReservationStatus.Finalized);
        reservation.FinalizedAt.Should().NotBeNull();

        var attempt = await verifyDb.RewriteAttempts.SingleAsync();
        attempt.Status.Should().Be(RewriteAttemptStatus.Succeeded);
        attempt.ResultJson.Should().Be(MinimalSuccessJson);
        attempt.ErrorCode.Should().BeNull();

        var costEntry = costLogger.Entries.Should().ContainSingle().Subject;
        costEntry.Status.Should().Be("succeeded");
        costEntry.ResultJson.Should().Be(MinimalSuccessJson);
        costEntry.ProviderCalls.Should().ContainSingle();
    }

    [Theory]
    [MemberData(nameof(InvalidSuccessJsonCases))]
    public async Task EngineContract_success_json_missing_required_field_releases_quota_with_provider_json_parse_failed(
        string invalidResultJson)
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var now = DateTimeOffset.UtcNow;
        var attemptId = await ReserveAttemptAsync(fixture, user.Id, $"engine-contract-invalid-{Guid.NewGuid():N}", now);
        var engine = new FakeRewriteEngineClient(new RewriteEngineResult(
            invalidResultJson,
            Success: true,
            ErrorCode: null,
            [Metric(success: true)]));
        var costLogger = new FakeRewriteCostLogger();

        await using var handlerDb = fixture.CreateContext();
        await CreateProcessHandler(handlerDb, engine, costLogger)
            .HandleAsync(new ProcessRewriteJobCommand(attemptId));

        await using var verifyDb = fixture.CreateContext();
        var period = await verifyDb.UsagePeriods.SingleAsync();
        period.UsedCount.Should().Be(0);
        period.ReservedCount.Should().Be(0);

        var reservation = await verifyDb.UsageReservations.SingleAsync();
        reservation.Status.Should().Be(UsageReservationStatus.Released);
        reservation.ReleasedAt.Should().NotBeNull();

        var attempt = await verifyDb.RewriteAttempts.SingleAsync();
        attempt.Status.Should().Be(RewriteAttemptStatus.Failed);
        attempt.ErrorCode.Should().Be(RewriteEngineErrorCodes.ProviderJsonParseFailed);

        var costEntry = costLogger.Entries.Should().ContainSingle().Subject;
        costEntry.Status.Should().Be("failed");
        costEntry.ErrorCode.Should().Be(RewriteEngineErrorCodes.ProviderJsonParseFailed);
    }

    [Fact]
    public async Task EngineContract_adapter_maps_unexpected_exception_to_provider_failed()
    {
        var adapter = new RewriteProviderEngineClient(
            new ThrowingRewriteProvider(new InvalidOperationException("provider failed")));

        var result = await adapter.RewriteAsync(Guid.NewGuid(), ValidRequest());

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(RewriteEngineErrorCodes.ProviderFailed);
        result.ProviderCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task EngineContract_adapter_maps_engine_cancellation_to_provider_timeout()
    {
        var adapter = new RewriteProviderEngineClient(
            new ThrowingRewriteProvider(new OperationCanceledException("provider timeout")));

        var result = await adapter.RewriteAsync(Guid.NewGuid(), ValidRequest(), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(RewriteEngineErrorCodes.ProviderTimeout);
        result.ProviderCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task EngineContract_v1_result_maps_succeeded_with_minimal_metadata()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var (user, token) = await SeedApiKeyUserAsync(fixture, "contract-v1-success");
        var attemptId = await SeedAttemptAsync(fixture, user.Id, RewriteAttemptStatus.Succeeded, MinimalSuccessJson);
        await using var db = fixture.CreateContext();
        var functions = CreateV1Functions(db, fixture.CreateContext);

        var result = await functions.GetRewriteResult(
            CreateV1Request(token),
            attemptId,
            CancellationToken.None);

        using var body = SerializeOkBody(result);
        body.RootElement.GetProperty("status").GetString().Should().Be("succeeded");
        body.RootElement.GetProperty("rewrittenText").GetString().Should().Be("Hi Sam, the report is ready.");
        var signal = body.RootElement.GetProperty("signal");
        signal.GetProperty("draft").GetDecimal().Should().Be(78);
        signal.GetProperty("rewrite").GetDecimal().Should().Be(24);
    }

    [Fact]
    public async Task EngineContract_v1_result_reports_engine_unavailable_when_naturalness_numbers_missing()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var (user, token) = await SeedApiKeyUserAsync(fixture, "contract-v1-missing-naturalness");
        var attemptId = await SeedAttemptAsync(fixture, user.Id, RewriteAttemptStatus.Succeeded, SuccessWithoutNaturalnessJson);
        await using var db = fixture.CreateContext();
        var functions = CreateV1Functions(db, fixture.CreateContext);

        var result = await functions.GetRewriteResult(
            CreateV1Request(token),
            attemptId,
            CancellationToken.None);

        using var body = SerializeOkBody(result);
        body.RootElement.GetProperty("status").GetString().Should().Be("failed");
        body.RootElement.GetProperty("error").GetProperty("code").GetString()
            .Should().Be(RewriteEngineErrorCodes.EngineUnavailableFallback);
    }

    [Fact]
    public async Task EngineContract_webhook_body_maps_succeeded_with_minimal_metadata()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var attemptId = await SeedWebhookDeliveryAsync(
            fixture,
            user.Id,
            RewriteAttemptStatus.Succeeded,
            MinimalSuccessJson);
        var sender = new RecordingWebhookDeliverySender();
        await using var handlerDb = fixture.CreateContext();

        var dispatched = await CreateWebhookHandler(handlerDb, sender).HandleAsync(
            new DispatchDueWebhooksCommand(DateTimeOffset.Parse("2026-06-06T02:00:00Z"), "contract-worker", 10));

        dispatched.Should().Be(1);
        using var body = JsonDocument.Parse(sender.Requests.Should().ContainSingle().Subject.RawBody);
        body.RootElement.GetProperty("id").GetGuid().Should().Be(attemptId);
        body.RootElement.GetProperty("status").GetString().Should().Be("succeeded");
        body.RootElement.GetProperty("rewrittenText").GetString().Should().Be("Hi Sam, the report is ready.");
        var signal = body.RootElement.GetProperty("signal");
        signal.GetProperty("draft").GetDecimal().Should().Be(78);
        signal.GetProperty("rewrite").GetDecimal().Should().Be(24);
    }

    [Fact]
    public async Task EngineContract_webhook_body_reports_engine_unavailable_when_naturalness_missing()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        await SeedWebhookDeliveryAsync(
            fixture,
            user.Id,
            RewriteAttemptStatus.Succeeded,
            SuccessWithoutNaturalnessJson);
        var sender = new RecordingWebhookDeliverySender();
        await using var handlerDb = fixture.CreateContext();

        var dispatched = await CreateWebhookHandler(handlerDb, sender).HandleAsync(
            new DispatchDueWebhooksCommand(DateTimeOffset.Parse("2026-06-06T02:00:00Z"), "contract-worker", 10));

        dispatched.Should().Be(1);
        using var body = JsonDocument.Parse(sender.Requests.Should().ContainSingle().Subject.RawBody);
        body.RootElement.GetProperty("status").GetString().Should().Be("failed");
        body.RootElement.GetProperty("error").GetProperty("code").GetString()
            .Should().Be(RewriteEngineErrorCodes.EngineUnavailableFallback);
    }

    [Fact]
    public async Task EngineContract_cost_logger_tolerates_absent_optional_metadata()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var attemptId = await SeedAttemptAsync(fixture, user.Id, RewriteAttemptStatus.Succeeded, MinimalSuccessJson);
        var now = DateTimeOffset.UtcNow;
        var logger = new RewriteCostLogger(fixture.CreateContext);

        await logger.WriteAsync(new RewriteCostLogEntry(
            attemptId,
            ValidRequest(),
            MinimalSuccessJson,
            [Metric(success: true)],
            "succeeded",
            null,
            now.AddSeconds(-1),
            now));

        await using var db = fixture.CreateContext();
        var log = await db.RewriteCostLogs.SingleAsync();
        log.StrategyVersion.Should().Be("unknown");
        log.Scenario.Should().Be("unknown");
        log.DraftAiLikePercent.Should().Be(78);
    }

    [Fact]
    public async Task EngineContract_cost_logger_writes_no_row_when_provider_calls_empty()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var attemptId = await SeedAttemptAsync(fixture, user.Id, RewriteAttemptStatus.Succeeded, MinimalSuccessJson);
        var now = DateTimeOffset.UtcNow;
        var logger = new RewriteCostLogger(fixture.CreateContext);

        await logger.WriteAsync(new RewriteCostLogEntry(
            attemptId,
            ValidRequest(),
            MinimalSuccessJson,
            [],
            "succeeded",
            null,
            now.AddSeconds(-1),
            now));

        await using var db = fixture.CreateContext();
        (await db.RewriteCostLogs.CountAsync()).Should().Be(0);
    }

    [Fact]
    public void EngineContract_quality_gate_codes_match_consumer_not_charged_set()
    {
        RewriteEngineErrorCodes.QualityGateNotCharged.Should().BeEquivalentTo(
            new[]
            {
                RewriteEngineErrorCodes.QualitySignalUnavailable,
                RewriteEngineErrorCodes.StructureGateFailed,
                RewriteEngineErrorCodes.NaturalnessGateFailed,
                RewriteEngineErrorCodes.FactGateFailed,
                RewriteEngineErrorCodes.PolicyIntentGateFailed,
            });
    }

    [Fact]
    public void EngineContract_error_code_constants_pin_wire_values()
    {
        var constants = new Dictionary<string, string>
        {
            [nameof(RewriteEngineErrorCodes.ProviderTimeout)] = RewriteEngineErrorCodes.ProviderTimeout,
            [nameof(RewriteEngineErrorCodes.ProviderFailed)] = RewriteEngineErrorCodes.ProviderFailed,
            [nameof(RewriteEngineErrorCodes.ProviderJsonParseFailed)] = RewriteEngineErrorCodes.ProviderJsonParseFailed,
            [nameof(RewriteEngineErrorCodes.RequestJsonParseFailed)] = RewriteEngineErrorCodes.RequestJsonParseFailed,
            [nameof(RewriteEngineErrorCodes.ReservationExpired)] = RewriteEngineErrorCodes.ReservationExpired,
            [nameof(RewriteEngineErrorCodes.ProcessingTimedOut)] = RewriteEngineErrorCodes.ProcessingTimedOut,
            [nameof(RewriteEngineErrorCodes.QualitySignalUnavailable)] = RewriteEngineErrorCodes.QualitySignalUnavailable,
            [nameof(RewriteEngineErrorCodes.NaturalnessGateFailed)] = RewriteEngineErrorCodes.NaturalnessGateFailed,
            [nameof(RewriteEngineErrorCodes.FactGateFailed)] = RewriteEngineErrorCodes.FactGateFailed,
            [nameof(RewriteEngineErrorCodes.StructureGateFailed)] = RewriteEngineErrorCodes.StructureGateFailed,
            [nameof(RewriteEngineErrorCodes.PolicyIntentGateFailed)] = RewriteEngineErrorCodes.PolicyIntentGateFailed,
            [nameof(RewriteEngineErrorCodes.RewriteQualityFailed)] = RewriteEngineErrorCodes.RewriteQualityFailed,
            [nameof(RewriteEngineErrorCodes.EngineUnavailableFallback)] = RewriteEngineErrorCodes.EngineUnavailableFallback,
        };

        constants.Should().Equal(new Dictionary<string, string>
        {
            [nameof(RewriteEngineErrorCodes.ProviderTimeout)] = "provider_timeout",
            [nameof(RewriteEngineErrorCodes.ProviderFailed)] = "provider_failed",
            [nameof(RewriteEngineErrorCodes.ProviderJsonParseFailed)] = "provider_json_parse_failed",
            [nameof(RewriteEngineErrorCodes.RequestJsonParseFailed)] = "request_json_parse_failed",
            [nameof(RewriteEngineErrorCodes.ReservationExpired)] = "reservation_expired",
            [nameof(RewriteEngineErrorCodes.ProcessingTimedOut)] = "processing_timed_out",
            [nameof(RewriteEngineErrorCodes.QualitySignalUnavailable)] = "quality_signal_unavailable",
            [nameof(RewriteEngineErrorCodes.NaturalnessGateFailed)] = "naturalness_gate_failed",
            [nameof(RewriteEngineErrorCodes.FactGateFailed)] = "fact_gate_failed",
            [nameof(RewriteEngineErrorCodes.StructureGateFailed)] = "structure_gate_failed",
            [nameof(RewriteEngineErrorCodes.PolicyIntentGateFailed)] = "policy_intent_gate_failed",
            [nameof(RewriteEngineErrorCodes.RewriteQualityFailed)] = "rewrite_quality_failed",
            [nameof(RewriteEngineErrorCodes.EngineUnavailableFallback)] = "engine_unavailable",
        });

        RewriteEngineErrorCodes.EngineEmittable.Should().BeEquivalentTo(
            new[]
            {
                RewriteEngineErrorCodes.QualitySignalUnavailable,
                RewriteEngineErrorCodes.NaturalnessGateFailed,
                RewriteEngineErrorCodes.FactGateFailed,
                RewriteEngineErrorCodes.StructureGateFailed,
                RewriteEngineErrorCodes.RewriteQualityFailed,
                RewriteEngineErrorCodes.ProviderTimeout,
                RewriteEngineErrorCodes.ProviderFailed,
            });
    }

    private static async Task<Guid> ReserveAttemptAsync(
        DbFixture fixture,
        Guid userId,
        string idempotencyKey,
        DateTimeOffset now)
    {
        await using var db = fixture.CreateContext();
        var result = await CreateReserveHandler(db).HandleAsync(new ReserveQuotaCommand(
            userId,
            idempotencyKey,
            $"hash-{idempotencyKey}",
            JsonSerializer.Serialize(ValidRequest()),
            "free:lifetime",
            3,
            now,
            TimeSpan.FromMinutes(10)));

        result.Kind.Should().Be(ReserveQuotaResultKind.Created);
        return result.AttemptId;
    }

    private static async Task<Guid> SeedAttemptAsync(
        DbFixture fixture,
        Guid userId,
        RewriteAttemptStatus status,
        string? resultJson,
        string? errorCode = null)
    {
        await using var db = fixture.CreateContext();
        var attempt = new RewriteAttempt
        {
            UserId = userId,
            IdempotencyKey = Guid.NewGuid().ToString("N"),
            RequestHash = Guid.NewGuid().ToString("N"),
            RequestJson = JsonSerializer.Serialize(ValidRequest()),
            Status = status,
            ResultJson = resultJson,
            ErrorCode = errorCode,
            CreatedAt = DateTimeOffset.Parse("2026-06-06T01:58:00Z"),
            CompletedAt = DateTimeOffset.Parse("2026-06-06T01:59:00Z"),
            ExpiresAt = DateTimeOffset.Parse("2026-06-06T02:08:00Z"),
        };
        db.RewriteAttempts.Add(attempt);
        await db.SaveChangesAsync();
        return attempt.Id;
    }

    private static async Task<(AppUser User, string Token)> SeedApiKeyUserAsync(
        DbFixture fixture,
        string label)
    {
        Environment.SetEnvironmentVariable("API_KEY_PEPPER", TestApiKeyPepper);
        var now = DateTimeOffset.UtcNow;
        var token = $"rmv_live_{label}_{Guid.NewGuid():N}";
        await using var db = fixture.CreateContext();
        var user = new AppUser
        {
            ExternalAuthUserId = $"clerk_{label}_{Guid.NewGuid():N}",
            Email = $"{label}@example.com",
            StripeCustomerId = $"cus_{label}",
            StripeSubscriptionId = $"sub_{label}",
            SubscriptionStatus = SubscriptionStatus.Active,
            CurrentPeriodEnd = now.AddDays(30),
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.AppUsers.Add(user);
        db.ApiKeys.Add(new ApiKey
        {
            User = user,
            Name = "Rewrite engine contract key",
            KeyHash = ApiKeyHashing.ComputeHash(token),
            Last4 = token[^4..],
            RateLimitPerMinute = 60,
            IsTest = false,
            CreatedAt = now,
            UpdatedAt = now,
        });
        await db.SaveChangesAsync();
        return (user, token);
    }

    private static async Task<Guid> SeedWebhookDeliveryAsync(
        DbFixture fixture,
        Guid userId,
        RewriteAttemptStatus status,
        string? resultJson)
    {
        await using var db = fixture.CreateContext();
        var now = DateTimeOffset.Parse("2026-06-06T01:59:00Z");
        var apiKey = new ApiKey
        {
            UserId = userId,
            Name = "Webhook contract key",
            KeyHash = Guid.NewGuid().ToString("N"),
            Last4 = "test",
            WebhookUrl = "https://93.184.216.34/rewrite",
            WebhookSecret = new string('c', 64),
            CreatedAt = now,
            UpdatedAt = now,
        };
        var attempt = new RewriteAttempt
        {
            UserId = userId,
            IdempotencyKey = Guid.NewGuid().ToString("N"),
            RequestHash = Guid.NewGuid().ToString("N"),
            RequestJson = JsonSerializer.Serialize(ValidRequest()),
            Status = status,
            ResultJson = resultJson,
            CreatedAt = now.AddMinutes(-1),
            CompletedAt = now,
            ExpiresAt = now.AddMinutes(10),
        };
        db.ApiKeys.Add(apiKey);
        db.RewriteAttempts.Add(attempt);
        db.WebhookDeliveries.Add(new WebhookDelivery
        {
            ApiKey = apiKey,
            RewriteAttempt = attempt,
            Url = apiKey.WebhookUrl,
            Status = WebhookDeliveryStatus.Pending,
            AttemptCount = 0,
            MaxAttempts = 5,
            CreatedAt = now,
            NextAttemptAt = now,
        });
        await db.SaveChangesAsync();
        return attempt.Id;
    }

    private static ReserveQuotaHandler CreateReserveHandler(AppDbContext db) =>
        new(
            new UsagePeriodRepository(db),
            new RewriteAttemptRepository(db),
            new UsageReservationRepository(db),
            new RewriteCreditRepository(db),
            new OutboxMessageRepository(db),
            new UnitOfWork(db),
            NullLogger<ReserveQuotaHandler>.Instance);

    private static ProcessRewriteJobHandler CreateProcessHandler(
        AppDbContext db,
        IRewriteEngineClient engine,
        IRewriteCostLogger costLogger) =>
        new(
            new RewriteAttemptRepository(db),
            new UsageReservationRepository(db),
            new UsagePeriodRepository(db),
            new RewriteCreditRepository(db),
            new UnitOfWork(db),
            engine,
            costLogger);

    private static DispatchDueWebhooksHandler CreateWebhookHandler(
        AppDbContext db,
        AppWebhookDeliverySender sender) =>
        new(
            new WebhookDeliveryRepository(db),
            sender,
            new UnitOfWork(db),
            NoOpBusinessMetrics.Instance);

    private static V1RewriteHttpFunctions CreateV1Functions(
        AppDbContext db,
        Func<AppDbContext> createContext)
    {
        var configuration = BuildConfiguration();
        var appUsers = new AppUserRepository(db);
        var usagePeriods = new UsagePeriodRepository(db);
        var rewriteAttempts = new RewriteAttemptRepository(db);
        var reservations = new UsageReservationRepository(db);
        var credits = new RewriteCreditRepository(db);
        var outboxMessages = new OutboxMessageRepository(db);
        var promoRedemptions = new PromoCodeRedemptionRepository(db);
        var promoCodes = new PromoCodeRepository(db);
        var apiKeys = new ApiKeyRepository(db);
        var apiKeyUsages = new ApiKeyUsageRepository(db);
        var usagePlans = new AccountUsagePlanProvider(configuration);
        var unitOfWork = new UnitOfWork(db);

        return new V1RewriteHttpFunctions(
            configuration,
            new ApiKeyAuthResolver(apiKeys, unitOfWork),
            appUsers,
            rewriteAttempts,
            apiKeyUsages,
            unitOfWork,
            new ApiKeyRateLimiter(createContext),
            new HasPaidApiEntitlementHandler(appUsers, credits),
            new CreateRewriteAttemptHandler(
                appUsers,
                usagePeriods,
                rewriteAttempts,
                reservations,
                credits,
                outboxMessages,
                unitOfWork,
                new NoopOutboxFastPathDispatcher()),
            new GetRewriteAttemptHandler(rewriteAttempts),
            new GetAccountSummaryHandler(
                appUsers,
                usagePeriods,
                credits,
                promoRedemptions,
                promoCodes,
                usagePlans,
                unitOfWork));
    }

    private static JsonDocument SerializeOkBody(IActionResult result)
    {
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        return JsonDocument.Parse(JsonSerializer.Serialize(ok.Value));
    }

    private static HttpRequest CreateV1Request(string token)
    {
        var context = new DefaultHttpContext();
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{}"));
        context.Request.ContentType = "application/json";
        context.Request.Headers.Authorization = $"Bearer {token}";
        return context.Request;
    }

    private static IConfiguration BuildConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ALLOW_HEADER_AUTH"] = "true",
                ["AZURE_FUNCTIONS_ENVIRONMENT"] = "Testing",
            })
            .Build();

    private static RewriteRequest ValidRequest() =>
        new(
            MessageToReplyTo: "Sam asked whether the report is ready.",
            RoughDraftReply: "Tell Sam the report is ready.",
            Audience: "Client",
            Purpose: "Send a status update.",
            WhatHappened: "The report is ready.",
            FactsToPreserve: "The report is ready.",
            Tone: "warm");

    private static RewriteEngineCallMetric Metric(
        bool success,
        string? errorCode = null) =>
        new(
            Provider: "openai-compatible",
            Role: "rewrite_model",
            Model: "deepseek-v4-pro",
            InputTokens: 100,
            OutputTokens: 40,
            Characters: null,
            LatencyMs: 12,
            Success: success,
            ErrorCode: errorCode);

    private sealed class FakeRewriteEngineClient(RewriteEngineResult result) : IRewriteEngineClient
    {
        public Task<RewriteEngineResult> RewriteAsync(
            Guid attemptId,
            RewriteRequest request,
            CancellationToken ct = default) =>
            Task.FromResult(result);
    }

    private sealed class FakeRewriteCostLogger : IRewriteCostLogger
    {
        private readonly List<RewriteCostLogEntry> _entries = [];

        public IReadOnlyList<RewriteCostLogEntry> Entries => _entries;

        public Task WriteAsync(
            RewriteCostLogEntry entry,
            CancellationToken ct = default)
        {
            _entries.Add(entry);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingRewriteProvider(Exception exception) : IRewriteProvider
    {
        public Task<RewriteProviderResult> RewriteAsync(
            Guid attemptId,
            RewriteRequest request,
            CancellationToken cancellationToken) =>
            Task.FromException<RewriteProviderResult>(exception);
    }

    private sealed class RecordingWebhookDeliverySender : AppWebhookDeliverySender
    {
        private readonly List<AppWebhookSendRequest> _requests = [];

        public IReadOnlyList<AppWebhookSendRequest> Requests => _requests;

        public Task<AppWebhookSendResult> SendAsync(
            AppWebhookSendRequest request,
            CancellationToken cancellationToken)
        {
            _requests.Add(request);
            return Task.FromResult(new AppWebhookSendResult((int)HttpStatusCode.OK));
        }
    }
}
