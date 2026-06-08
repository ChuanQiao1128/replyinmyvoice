using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Functions.Functions;
using ReplyInMyVoice.Infrastructure.Data;
using ReplyInMyVoice.Infrastructure.Services;

namespace ReplyInMyVoice.Tests;

[Collection("ApiKeyPepper")]
public sealed class ApiInputHardeningTests
{
    private const string TestApiKeyPepper = "api-input-hardening-test-pepper";

    public static TheoryData<string, string?, HttpStatusCode, string> InvalidRewriteInputCases => new()
    {
        { string.Empty, null, HttpStatusCode.BadRequest, "invalid_request" },
        { "not-json", null, HttpStatusCode.BadRequest, "invalid_request" },
        { """{"audience":"client"}""", null, HttpStatusCode.BadRequest, "invalid_request" },
        { """{"draft":"     "}""", null, HttpStatusCode.BadRequest, "invalid_request" },
        { JsonSerializer.Serialize(new { draft = string.Join(" ", Enumerable.Repeat("word", 301)) }), null, HttpStatusCode.BadRequest, "input_too_long" },
        { JsonSerializer.Serialize(new { draft = new string('a', 2401) }), null, HttpStatusCode.BadRequest, "input_too_long" },
        { JsonSerializer.Serialize(new { draft = ValidV1Draft() }), new string('k', 121), HttpStatusCode.BadRequest, "invalid_request" },
    };

    public static TheoryData<string> InvalidUsageDaysQueries => new()
    {
        "?days=0",
        "?days=-1",
        "?days=500",
        "?days=abc",
    };

    [Theory]
    [MemberData(nameof(InvalidRewriteInputCases))]
    public async Task V1_rewrite_submit_returns_contract_error_for_invalid_boundary_input(
        string bodyJson,
        string? idempotencyKey,
        HttpStatusCode expectedStatus,
        string expectedCode)
    {
        await using var fixture = await DbFixture.CreateAsync();
        var (_, token) = await SeedApiKeyUserAsync(
            fixture,
            $"clerk_api_input_{Guid.NewGuid():N}",
            SubscriptionStatus.Active,
            DateTimeOffset.UtcNow.AddDays(30),
            isTest: true);
        await using var db = fixture.CreateContext();
        var functions = CreateV1Functions(db, fixture.CreateContext);

        var result = await functions.SubmitRewrite(
            CreateV1Request(token, bodyJson, idempotencyKey),
            CancellationToken.None);

        AssertContractError(result, expectedStatus, expectedCode);
    }

    [Fact]
    public async Task V1_rewrite_submit_ignores_unknown_json_fields()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var (_, token) = await SeedApiKeyUserAsync(
            fixture,
            $"clerk_api_extra_{Guid.NewGuid():N}",
            SubscriptionStatus.Active,
            DateTimeOffset.UtcNow.AddDays(30),
            isTest: true);
        await using var db = fixture.CreateContext();
        var functions = CreateV1Functions(db, fixture.CreateContext);
        var bodyJson = JsonSerializer.Serialize(new
        {
            draft = ValidV1Draft(),
            unexpected = new
            {
                nested = true,
            },
        });

        var result = await functions.SubmitRewrite(
            CreateV1Request(token, bodyJson, "unknown-extra-fields"),
            CancellationToken.None);

        var accepted = result.Should().BeOfType<AcceptedResult>().Subject;
        accepted.StatusCode.Should().Be(StatusCodes.Status202Accepted);
    }

    [Theory]
    [MemberData(nameof(InvalidUsageDaysQueries))]
    public async Task Api_usage_series_rejects_invalid_days_query_with_contract_error(string queryString)
    {
        await using var fixture = await DbFixture.CreateAsync();
        var functions = CreateUsageFunctions(fixture.CreateContext);

        var result = await functions.GetApiUsageSeries(
            CreateUsageRequest(queryString),
            CancellationToken.None);

        AssertContractError(result, HttpStatusCode.BadRequest, "invalid_request");
    }

    [Fact]
    public async Task V1_rewrite_submit_requires_api_key_with_contract_error()
    {
        await using var fixture = await DbFixture.CreateAsync();
        await using var db = fixture.CreateContext();
        var functions = CreateV1Functions(db, fixture.CreateContext);

        var result = await functions.SubmitRewrite(
            CreateV1Request(token: null, JsonSerializer.Serialize(new { draft = ValidV1Draft() })),
            CancellationToken.None);

        AssertContractError(result, HttpStatusCode.Unauthorized, "invalid_key");
    }

    [Fact]
    public async Task V1_rewrite_submit_requires_paid_plan_for_live_key_with_contract_error()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var (_, token) = await SeedApiKeyUserAsync(
            fixture,
            $"clerk_api_unpaid_{Guid.NewGuid():N}",
            SubscriptionStatus.Inactive,
            currentPeriodEnd: null,
            isTest: false);
        await using var db = fixture.CreateContext();
        var functions = CreateV1Functions(db, fixture.CreateContext);

        var result = await functions.SubmitRewrite(
            CreateV1Request(token, JsonSerializer.Serialize(new { draft = ValidV1Draft() }), "paid-plan-required"),
            CancellationToken.None);

        AssertContractError(result, HttpStatusCode.PaymentRequired, "api_requires_paid_plan");
    }

    [Fact]
    public async Task V1_rewrite_submit_returns_conflict_contract_for_reused_idempotency_key_with_different_draft()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var (_, token) = await SeedApiKeyUserAsync(
            fixture,
            $"clerk_api_conflict_{Guid.NewGuid():N}",
            SubscriptionStatus.Active,
            DateTimeOffset.UtcNow.AddDays(30),
            isTest: true);
        await using var db = fixture.CreateContext();
        var functions = CreateV1Functions(db, fixture.CreateContext);

        var first = await functions.SubmitRewrite(
            CreateV1Request(token, JsonSerializer.Serialize(new { draft = ValidV1Draft() }), "same-key"),
            CancellationToken.None);
        first.Should().BeOfType<AcceptedResult>();

        var second = await functions.SubmitRewrite(
            CreateV1Request(token, JsonSerializer.Serialize(new { draft = AlternateValidV1Draft() }), "same-key"),
            CancellationToken.None);

        AssertContractError(second, HttpStatusCode.Conflict, "idempotency_conflict");
    }

    [Fact]
    public async Task V1_rewrite_submit_returns_rate_limit_contract()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var (_, token) = await SeedApiKeyUserAsync(
            fixture,
            $"clerk_api_limited_{Guid.NewGuid():N}",
            SubscriptionStatus.Active,
            DateTimeOffset.UtcNow.AddDays(30),
            rateLimitPerMinute: 1,
            isTest: true);
        await using var db = fixture.CreateContext();
        var functions = CreateV1Functions(db, fixture.CreateContext);

        var first = await functions.SubmitRewrite(
            CreateV1Request(token, JsonSerializer.Serialize(new { draft = ValidV1Draft() }), "first-limited-call"),
            CancellationToken.None);
        first.Should().BeOfType<AcceptedResult>();

        var second = await functions.SubmitRewrite(
            CreateV1Request(token, JsonSerializer.Serialize(new { draft = AlternateValidV1Draft() }), "second-limited-call"),
            CancellationToken.None);

        AssertContractError(second, HttpStatusCode.TooManyRequests, "rate_limited");
    }

    private static V1RewriteHttpFunctions CreateV1Functions(
        AppDbContext db,
        Func<AppDbContext> createContext)
    {
        var accountService = new AccountService(createContext);
        var quotaService = new QuotaService(createContext);
        return new V1RewriteHttpFunctions(
            BuildConfiguration(),
            db,
            new ApiKeyRateLimiter(createContext),
            accountService,
            new RewriteRequestService(createContext, quotaService));
    }

    private static ApiUsageHttpFunctions CreateUsageFunctions(Func<AppDbContext> createContext)
    {
        var accountService = new AccountService(createContext);
        return new ApiUsageHttpFunctions(
            BuildConfiguration(),
            accountService,
            new ApiKeyUsageQueryService(createContext, accountService));
    }

    private static async Task<(AppUser User, string Token)> SeedApiKeyUserAsync(
        DbFixture fixture,
        string externalAuthUserId,
        SubscriptionStatus subscriptionStatus,
        DateTimeOffset? currentPeriodEnd,
        int rateLimitPerMinute = 60,
        bool isTest = false)
    {
        Environment.SetEnvironmentVariable("API_KEY_PEPPER", TestApiKeyPepper);
        var now = DateTimeOffset.UtcNow;
        var tokenPrefix = isTest ? "rmv_test_" : "rmv_live_";
        var token = $"{tokenPrefix}{externalAuthUserId}_token";
        await using var db = fixture.CreateContext();
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
            Name = "API input hardening key",
            KeyHash = ComputeApiKeyHash(token, TestApiKeyPepper),
            Last4 = token[^4..],
            IsTest = isTest,
            RateLimitPerMinute = rateLimitPerMinute,
            CreatedAt = now,
            UpdatedAt = now,
        });

        await db.SaveChangesAsync();
        return (user, token);
    }

    private static HttpRequest CreateV1Request(
        string? token,
        string bodyJson,
        string? idempotencyKey = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(bodyJson));
        context.Request.ContentType = "application/json";
        if (!string.IsNullOrWhiteSpace(token))
        {
            context.Request.Headers.Authorization = $"Bearer {token}";
        }

        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            context.Request.Headers["Idempotency-Key"] = idempotencyKey;
        }

        return context.Request;
    }

    private static HttpRequest CreateUsageRequest(string queryString)
    {
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString(queryString);
        context.Request.Headers["X-External-User-Id"] = $"entra-api-input-{Guid.NewGuid():N}";
        context.Request.Headers["X-User-Email"] = "api-input@example.com";
        return context.Request;
    }

    private static void AssertContractError(
        IActionResult result,
        HttpStatusCode expectedStatus,
        string expectedCode)
    {
        var objectResult = result.Should().BeAssignableTo<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be((int)expectedStatus);
        objectResult.StatusCode.Should().NotBe(StatusCodes.Status500InternalServerError);
        objectResult.Value.Should().NotBeNull();

        var json = JsonSerializer.Serialize(objectResult.Value);
        using var document = JsonDocument.Parse(json);
        var error = document.RootElement.GetProperty("error");
        error.GetProperty("code").GetString().Should().Be(expectedCode);
        error.GetProperty("message").GetString().Should().NotBeNullOrWhiteSpace();
    }

    private static IConfiguration BuildConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ALLOW_HEADER_AUTH"] = "true",
                ["AZURE_FUNCTIONS_ENVIRONMENT"] = "Testing",
            })
            .Build();

    private static string ValidV1Draft() =>
        "Please let the client know the report is still being checked and I will send a clear update soon.";

    private static string AlternateValidV1Draft() =>
        "Please tell the customer I received the note and will send a clearer answer after reviewing it.";

    private static string ComputeApiKeyHash(string plaintext, string? pepper)
    {
        var material = string.IsNullOrWhiteSpace(pepper)
            ? plaintext
            : string.Concat(pepper, plaintext);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(material));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
