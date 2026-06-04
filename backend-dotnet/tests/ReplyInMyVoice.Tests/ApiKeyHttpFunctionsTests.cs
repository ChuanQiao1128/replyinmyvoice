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

public sealed class ApiKeyHttpFunctionsTests
{
    private const string TestPepper = "api-key-http-functions-test-pepper";

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

        await using (var db = fixture.CreateContext())
        {
            var stored = await db.ApiKeys.SingleAsync(x => x.Id == createBody.Id);
            stored.KeyHash.Should().Be(ApiKeyService.ComputeHash(createBody.Key));
            stored.KeyHash.Should().NotBe(createBody.Key);
            stored.Last4.Should().Be(createBody.Key[^4..]);
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
        item.RevokedAt.Should().BeNull();

        var listJson = JsonSerializer.Serialize(listBody, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        listJson.Should().NotContain(createBody.Key);
        listJson.Should().NotContain(ApiKeyService.ComputeHash(createBody.Key));
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
