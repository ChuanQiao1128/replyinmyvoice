using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Functions.Auth;
using ReplyInMyVoice.Infrastructure.Data;
using ReplyInMyVoice.Infrastructure.Services;

namespace ReplyInMyVoice.Tests;

[Collection("ApiKeyPepper")]
public sealed class ApiKeyAuthResolverTests
{
    private const string TestPepper = "api-key-test-pepper";

    [Fact]
    public async Task ResolveUserIdAsync_returns_user_id_for_active_key_and_updates_last_used_at()
    {
        Environment.SetEnvironmentVariable("API_KEY_PEPPER", TestPepper);
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var token = "rmv_live_valid_resolver_key";
        var now = DateTimeOffset.Parse("2026-06-04T12:00:00Z");

        await SeedApiKeyAsync(fixture, user.Id, token);
        await using var db = fixture.CreateContext();
        var request = CreateRequest(token);

        var resolvedUserId = await ApiKeyAuthResolver.ResolveUserIdAsync(
            request,
            db,
            now,
            CancellationToken.None);

        resolvedUserId.Should().Be(user.Id);
        var stored = await db.ApiKeys.SingleAsync();
        stored.LastUsedAt.Should().Be(now);
    }

    [Fact]
    public async Task ResolveAsync_returns_test_key_flag_for_active_test_key()
    {
        Environment.SetEnvironmentVariable("API_KEY_PEPPER", TestPepper);
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var token = "rmv_test_valid_resolver_key";
        var now = DateTimeOffset.Parse("2026-06-04T12:00:00Z");

        await SeedApiKeyAsync(fixture, user.Id, token, isTest: true);
        await using var db = fixture.CreateContext();
        var request = CreateRequest(token);

        var resolved = await ApiKeyAuthResolver.ResolveAsync(
            request,
            db,
            now,
            CancellationToken.None);

        resolved.UserId.Should().Be(user.Id);
        resolved.IsTest.Should().BeTrue();
        var stored = await db.ApiKeys.SingleAsync();
        stored.LastUsedAt.Should().Be(now);
    }

    [Fact]
    public async Task ResolveUserIdAsync_returns_null_for_unknown_token()
    {
        Environment.SetEnvironmentVariable("API_KEY_PEPPER", TestPepper);
        await using var fixture = await DbFixture.CreateAsync();
        var now = DateTimeOffset.Parse("2026-06-04T12:00:00Z");
        await using var db = fixture.CreateContext();
        var request = CreateRequest("rmv_live_missing_resolver_key");

        var resolvedUserId = await ApiKeyAuthResolver.ResolveUserIdAsync(
            request,
            db,
            now,
            CancellationToken.None);

        resolvedUserId.Should().BeNull();
    }

    [Fact]
    public async Task ResolveUserIdAsync_returns_null_for_revoked_key()
    {
        Environment.SetEnvironmentVariable("API_KEY_PEPPER", TestPepper);
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var token = "rmv_live_revoked_resolver_key";
        var now = DateTimeOffset.Parse("2026-06-04T12:00:00Z");

        await SeedApiKeyAsync(
            fixture,
            user.Id,
            token,
            revokedAt: now.AddMinutes(-5));
        await using var db = fixture.CreateContext();
        var request = CreateRequest(token);

        var resolvedUserId = await ApiKeyAuthResolver.ResolveUserIdAsync(
            request,
            db,
            now,
            CancellationToken.None);

        resolvedUserId.Should().BeNull();
        var stored = await db.ApiKeys.SingleAsync();
        stored.LastUsedAt.Should().BeNull();
    }

    [Fact]
    public async Task ResolveUserIdAsync_returns_null_for_expired_key()
    {
        Environment.SetEnvironmentVariable("API_KEY_PEPPER", TestPepper);
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var token = "rmv_live_expired_resolver_key";
        var now = DateTimeOffset.Parse("2026-06-04T12:00:00Z");

        await SeedApiKeyAsync(
            fixture,
            user.Id,
            token,
            expiresAt: now.AddSeconds(-1));
        await using var db = fixture.CreateContext();
        var request = CreateRequest(token);

        var resolvedUserId = await ApiKeyAuthResolver.ResolveUserIdAsync(
            request,
            db,
            now,
            CancellationToken.None);

        resolvedUserId.Should().BeNull();
        var stored = await db.ApiKeys.SingleAsync();
        stored.LastUsedAt.Should().BeNull();
    }

    [Fact]
    public async Task ResolveUserIdAsync_returns_null_when_authorization_header_is_missing()
    {
        Environment.SetEnvironmentVariable("API_KEY_PEPPER", TestPepper);
        await using var fixture = await DbFixture.CreateAsync();
        var now = DateTimeOffset.Parse("2026-06-04T12:00:00Z");
        await using var db = fixture.CreateContext();

        var resolvedUserId = await ApiKeyAuthResolver.ResolveUserIdAsync(
            new DefaultHttpContext().Request,
            db,
            now,
            CancellationToken.None);

        resolvedUserId.Should().BeNull();
    }

    [Fact]
    public async Task ResolveUserIdAsync_returns_null_for_unknown_token_prefix()
    {
        Environment.SetEnvironmentVariable("API_KEY_PEPPER", TestPepper);
        await using var fixture = await DbFixture.CreateAsync();
        var now = DateTimeOffset.Parse("2026-06-04T12:00:00Z");
        await using var db = fixture.CreateContext();
        var request = CreateRequest("rmv_preview_resolver_key");

        var resolvedUserId = await ApiKeyAuthResolver.ResolveUserIdAsync(
            request,
            db,
            now,
            CancellationToken.None);

        resolvedUserId.Should().BeNull();
    }

    private static async Task SeedApiKeyAsync(
        DbFixture fixture,
        Guid userId,
        string token,
        DateTimeOffset? expiresAt = null,
        DateTimeOffset? revokedAt = null,
        bool isTest = false)
    {
        var now = DateTimeOffset.Parse("2026-06-04T11:00:00Z");
        await using var db = fixture.CreateContext();
        db.ApiKeys.Add(new ApiKey
        {
            UserId = userId,
            Name = "Resolver key",
            KeyHash = ApiKeyHashing.ComputeHash(token),
            Last4 = token[^4..],
            IsTest = isTest,
            ExpiresAt = expiresAt,
            RevokedAt = revokedAt,
            CreatedAt = now,
            UpdatedAt = now,
        });
        await db.SaveChangesAsync();
    }

    private static HttpRequest CreateRequest(string token)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = $"Bearer {token}";
        return context.Request;
    }
}
