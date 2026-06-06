using System.Net;
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
public sealed class ApiKeyHttpFunctionsTests
{
    private const string TestPepper = "api-key-test-pepper";

    [Fact]
    public async Task CreateKey_returns_plaintext_once_and_list_returns_masked_keys()
    {
        Environment.SetEnvironmentVariable("API_KEY_PEPPER", TestPepper);
        await using var fixture = await DbFixture.CreateAsync();
        var functions = CreateFunctions(fixture.CreateContext);

        var createResult = await functions.CreateApiKey(
            CreateRequest("entra-key-owner", "owner@example.com", new { name = "Primary API key" }),
            CancellationToken.None);

        var created = createResult.Should().BeOfType<CreatedResult>().Subject;
        created.StatusCode.Should().Be((int)HttpStatusCode.Created);
        var createBody = created.Value.Should().BeOfType<ApiKeyCreateResponse>().Subject;
        createBody.Name.Should().Be("Primary API key");
        createBody.Key.Should().StartWith("rmv_live_");
        createBody.IsTest.Should().BeFalse();

        await using (var db = fixture.CreateContext())
        {
            var stored = await db.ApiKeys.SingleAsync(x => x.Id == createBody.Id);
            stored.KeyHash.Should().Be(ApiKeyService.ComputeHash(createBody.Key));
            stored.KeyHash.Should().NotBe(createBody.Key);
            stored.Last4.Should().Be(createBody.Key[^4..]);
            stored.IsTest.Should().BeFalse();
        }

        var listResult = await functions.ListApiKeys(
            CreateRequest("entra-key-owner", "owner@example.com"),
            CancellationToken.None);

        var ok = listResult.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be((int)HttpStatusCode.OK);
        var listBody = ok.Value.Should().BeAssignableTo<IReadOnlyList<ApiKeyListResponse>>().Subject;
        var item = listBody.Should().ContainSingle().Subject;
        item.Id.Should().Be(createBody.Id);
        item.Name.Should().Be("Primary API key");
        item.MaskedKey.Should().StartWith("rmv_live_");
        item.MaskedKey.Should().EndWith(createBody.Key[^4..]);
        item.IsTest.Should().BeFalse();
        item.RevokedAt.Should().BeNull();

        var listJson = JsonSerializer.Serialize(listBody, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        listJson.Should().NotContain(createBody.Key);
        listJson.Should().NotContain(ApiKeyService.ComputeHash(createBody.Key));
    }

    [Fact]
    public async Task CreateKey_can_create_test_key_and_list_labels_it()
    {
        Environment.SetEnvironmentVariable("API_KEY_PEPPER", TestPepper);
        await using var fixture = await DbFixture.CreateAsync();
        var functions = CreateFunctions(fixture.CreateContext);

        var createResult = await functions.CreateApiKey(
            CreateRequest("entra-test-key-owner", "owner@example.com", new { name = "Sandbox client", test = true }),
            CancellationToken.None);

        var created = createResult.Should().BeOfType<CreatedResult>().Subject;
        var createBody = created.Value.Should().BeOfType<ApiKeyCreateResponse>().Subject;
        createBody.Name.Should().Be("Sandbox client");
        createBody.Key.Should().StartWith("rmv_test_");
        createBody.IsTest.Should().BeTrue();

        await using (var db = fixture.CreateContext())
        {
            var stored = await db.ApiKeys.SingleAsync(x => x.Id == createBody.Id);
            stored.IsTest.Should().BeTrue();
            stored.KeyHash.Should().Be(ApiKeyService.ComputeHash(createBody.Key));
            stored.Last4.Should().Be(createBody.Key[^4..]);
        }

        var listResult = await functions.ListApiKeys(
            CreateRequest("entra-test-key-owner", "owner@example.com"),
            CancellationToken.None);

        var ok = listResult.Should().BeOfType<OkObjectResult>().Subject;
        var listBody = ok.Value.Should().BeAssignableTo<IReadOnlyList<ApiKeyListResponse>>().Subject;
        var item = listBody.Should().ContainSingle().Subject;
        item.IsTest.Should().BeTrue();
        item.MaskedKey.Should().StartWith("rmv_test_");
        item.MaskedKey.Should().EndWith(createBody.Key[^4..]);
    }

    [Fact]
    public async Task RevokeKey_sets_revoked_at_for_owner_and_returns_not_found_for_other_user()
    {
        Environment.SetEnvironmentVariable("API_KEY_PEPPER", TestPepper);
        await using var fixture = await DbFixture.CreateAsync();
        var owner = await SeedUserAsync(fixture, "entra-key-revoke-owner");
        await SeedUserAsync(fixture, "entra-key-revoke-other");
        var apiKeyService = new ApiKeyService(fixture.CreateContext);
        var generated = await apiKeyService.GenerateAsync(
            owner.Id,
            "Server key",
            CancellationToken.None);
        var functions = CreateFunctions(fixture.CreateContext);

        var otherResult = await functions.RevokeApiKey(
            CreateRequest("entra-key-revoke-other", "other@example.com"),
            generated.Id,
            CancellationToken.None);

        otherResult.Should().BeOfType<NotFoundResult>();
        await using (var unchangedDb = fixture.CreateContext())
        {
            var unchanged = await unchangedDb.ApiKeys.SingleAsync(x => x.Id == generated.Id);
            unchanged.RevokedAt.Should().BeNull();
        }

        var ownerResult = await functions.RevokeApiKey(
            CreateRequest("entra-key-revoke-owner", "owner@example.com"),
            generated.Id,
            CancellationToken.None);

        ownerResult.Should().BeOfType<NoContentResult>();
        await using var db = fixture.CreateContext();
        var stored = await db.ApiKeys.SingleAsync(x => x.Id == generated.Id);
        stored.RevokedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task RotateKey_returns_plaintext_replacement_and_revokes_only_owner_key()
    {
        Environment.SetEnvironmentVariable("API_KEY_PEPPER", TestPepper);
        await using var fixture = await DbFixture.CreateAsync();
        var owner = await SeedUserAsync(fixture, "entra-key-rotate-owner");
        await SeedUserAsync(fixture, "entra-key-rotate-other");
        var apiKeyService = new ApiKeyService(fixture.CreateContext);
        var generated = await apiKeyService.GenerateAsync(
            owner.Id,
            "Server key",
            CancellationToken.None);
        var functions = CreateFunctions(fixture.CreateContext);

        var otherResult = await functions.RotateApiKey(
            CreateRequest("entra-key-rotate-other", "other@example.com"),
            generated.Id,
            CancellationToken.None);

        otherResult.Should().BeOfType<NotFoundResult>();
        await using (var unchangedDb = fixture.CreateContext())
        {
            var unchanged = await unchangedDb.ApiKeys.SingleAsync(x => x.Id == generated.Id);
            unchanged.RevokedAt.Should().BeNull();
        }

        var ownerResult = await functions.RotateApiKey(
            CreateRequest("entra-key-rotate-owner", "owner@example.com"),
            generated.Id,
            CancellationToken.None);

        var created = ownerResult.Should().BeOfType<CreatedResult>().Subject;
        created.StatusCode.Should().Be((int)HttpStatusCode.Created);
        var body = created.Value.Should().BeOfType<ApiKeyCreateResponse>().Subject;
        body.Id.Should().NotBe(generated.Id);
        body.Name.Should().Be("Server key");
        body.Key.Should().StartWith("rmv_live_");
        body.IsTest.Should().BeFalse();

        await using var db = fixture.CreateContext();
        var oldKey = await db.ApiKeys.SingleAsync(x => x.Id == generated.Id);
        var newKey = await db.ApiKeys.SingleAsync(x => x.Id == body.Id);
        oldKey.RevokedAt.Should().NotBeNull();
        newKey.RevokedAt.Should().BeNull();
        newKey.IsTest.Should().BeFalse();
        newKey.KeyHash.Should().Be(ApiKeyService.ComputeHash(body.Key));

        var responseJson = JsonSerializer.Serialize(body, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        responseJson.Should().NotContain(ApiKeyService.ComputeHash(body.Key));
    }

    [Fact]
    public async Task SetWebhookUrl_generates_signing_value_once_and_list_returns_url_only()
    {
        Environment.SetEnvironmentVariable("API_KEY_PEPPER", TestPepper);
        await using var fixture = await DbFixture.CreateAsync();
        var owner = await SeedUserAsync(fixture, "entra-key-webhook-owner");
        var apiKeyService = new ApiKeyService(fixture.CreateContext);
        var generated = await apiKeyService.GenerateAsync(
            owner.Id,
            "Server key",
            CancellationToken.None);
        var functions = CreateFunctions(fixture.CreateContext);

        var setResult = await functions.SetApiKeyWebhook(
            CreateRequest(
                "entra-key-webhook-owner",
                "owner@example.com",
                new { webhookUrl = "https://listener.example.test/rewrite" }),
            generated.Id,
            CancellationToken.None);

        var ok = setResult.Should().BeOfType<OkObjectResult>().Subject;
        var setBody = ok.Value.Should().BeOfType<ApiKeyWebhookResponse>().Subject;
        setBody.Id.Should().Be(generated.Id);
        setBody.WebhookUrl.Should().Be("https://listener.example.test/rewrite");
        setBody.WebhookSecret.Should().NotBeNullOrWhiteSpace();

        await using (var db = fixture.CreateContext())
        {
            var stored = await db.ApiKeys.SingleAsync(x => x.Id == generated.Id);
            stored.WebhookUrl.Should().Be("https://listener.example.test/rewrite");
            stored.WebhookSecret.Should().Be(setBody.WebhookSecret);
        }

        var listResult = await functions.ListApiKeys(
            CreateRequest("entra-key-webhook-owner", "owner@example.com"),
            CancellationToken.None);

        var listBody = listResult
            .Should()
            .BeOfType<OkObjectResult>()
            .Subject
            .Value
            .Should()
            .BeAssignableTo<IReadOnlyList<ApiKeyListResponse>>()
            .Subject;
        var item = listBody.Should().ContainSingle().Subject;
        item.WebhookUrl.Should().Be("https://listener.example.test/rewrite");

        var listJson = JsonSerializer.Serialize(listBody, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        listJson.Should().NotContain(setBody.WebhookSecret);
    }

    [Fact]
    public async Task ClearWebhook_removes_url_and_signing_value_for_owner_only()
    {
        Environment.SetEnvironmentVariable("API_KEY_PEPPER", TestPepper);
        await using var fixture = await DbFixture.CreateAsync();
        var owner = await SeedUserAsync(fixture, "entra-key-webhook-clear-owner");
        await SeedUserAsync(fixture, "entra-key-webhook-clear-other");
        var apiKeyService = new ApiKeyService(fixture.CreateContext);
        var generated = await apiKeyService.GenerateAsync(
            owner.Id,
            "Server key",
            CancellationToken.None);
        await using (var seedDb = fixture.CreateContext())
        {
            var key = await seedDb.ApiKeys.SingleAsync(x => x.Id == generated.Id);
            key.WebhookUrl = "https://listener.example.test/rewrite";
            key.WebhookSecret = "unit-webhook-signing-value";
            await seedDb.SaveChangesAsync();
        }

        var functions = CreateFunctions(fixture.CreateContext);

        var otherResult = await functions.ClearApiKeyWebhook(
            CreateRequest("entra-key-webhook-clear-other", "other@example.com"),
            generated.Id,
            CancellationToken.None);

        otherResult.Should().BeOfType<NotFoundResult>();
        await using (var unchangedDb = fixture.CreateContext())
        {
            var unchanged = await unchangedDb.ApiKeys.SingleAsync(x => x.Id == generated.Id);
            unchanged.WebhookUrl.Should().Be("https://listener.example.test/rewrite");
            unchanged.WebhookSecret.Should().Be("unit-webhook-signing-value");
        }

        var ownerResult = await functions.ClearApiKeyWebhook(
            CreateRequest("entra-key-webhook-clear-owner", "owner@example.com"),
            generated.Id,
            CancellationToken.None);

        ownerResult.Should().BeOfType<NoContentResult>();
        await using var db = fixture.CreateContext();
        var stored = await db.ApiKeys.SingleAsync(x => x.Id == generated.Id);
        stored.WebhookUrl.Should().BeNull();
        stored.WebhookSecret.Should().BeNull();
    }

    private static ApiKeyHttpFunctions CreateFunctions(Func<AppDbContext> createContext) =>
        new(
            BuildConfiguration(),
            new AccountService(createContext),
            new ApiKeyService(createContext));

    private static HttpRequest CreateRequest(
        string externalAuthUserId,
        string email,
        object? body = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-External-User-Id"] = externalAuthUserId;
        context.Request.Headers["X-User-Email"] = email;

        if (body is not null)
        {
            var json = JsonSerializer.Serialize(body, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(json));
            context.Request.ContentType = "application/json";
        }

        return context.Request;
    }

    private static async Task<AppUser> SeedUserAsync(DbFixture fixture, string externalAuthUserId)
    {
        await using var db = fixture.CreateContext();
        var user = new AppUser
        {
            ExternalAuthUserId = externalAuthUserId,
            Email = $"{externalAuthUserId}@example.com",
            SubscriptionStatus = SubscriptionStatus.Inactive,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        db.AppUsers.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private static IConfiguration BuildConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ALLOW_HEADER_AUTH"] = "true",
                ["AZURE_FUNCTIONS_ENVIRONMENT"] = "Testing",
            })
            .Build();
}
