using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Application.Common;
using ReplyInMyVoice.Application.UseCases.ApiKey;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Functions.Auth;
using ReplyInMyVoice.Infrastructure.Data;
using ReplyInMyVoice.Infrastructure.Repositories;
using ReplyInMyVoice.Infrastructure.Services;

namespace ReplyInMyVoice.Tests;

[Collection("ApiKeyPepper")]
public sealed class P1_09_ApiKeyPepperRotationTests
{
    private const int CurrentVersion = 2;
    private const int PreviousVersion = 1;
    private const string CurrentPepper = "api-key-current-pepper";
    private const string PreviousPepper = "api-key-previous-pepper";

    [Fact]
    public async Task ResolveAsync_validates_against_previous_pepper_and_marks_rehash_pending()
    {
        ConfigurePepperRotation();
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var token = "rmv_live_previous_pepper_key";
        var now = DateTimeOffset.Parse("2026-06-19T12:00:00Z");
        var previousHash = ApiKeyHashing.ComputeHashWithVersion(token, PreviousVersion);

        await SeedApiKeyAsync(
            fixture,
            user.Id,
            token,
            previousHash,
            PreviousVersion);
        await using var db = fixture.CreateContext();
        var resolver = CreateResolver(db);

        var resolved = await resolver.ResolveAsync(
            CreateRequest(token),
            now,
            CancellationToken.None);

        resolved.UserId.Should().Be(user.Id);
        var stored = await db.ApiKeys.SingleAsync();
        stored.RehashPending.Should().BeTrue();
        stored.PepperVersion.Should().Be(CurrentVersion);
        stored.KeyHash.Should().Be(ApiKeyHashing.ComputeHashWithVersion(token, CurrentVersion));
        stored.LastUsedAt.Should().Be(now);
    }

    [Fact]
    public async Task ResolveAsync_validates_existing_key_without_pepper_version_remains_backward_compatible()
    {
        ConfigureCurrentOnly("api-key-current-only-pepper", 7);
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var token = "rmv_live_existing_unversioned_key";
        var now = DateTimeOffset.Parse("2026-06-19T12:30:00Z");

        await SeedApiKeyAsync(
            fixture,
            user.Id,
            token,
            ApiKeyHashing.ComputeHashWithVersion(token, 7),
            pepperVersion: null);
        await using var db = fixture.CreateContext();
        var resolver = CreateResolver(db);

        var resolved = await resolver.ResolveAsync(
            CreateRequest(token),
            now,
            CancellationToken.None);

        resolved.UserId.Should().Be(user.Id);
        var stored = await db.ApiKeys.SingleAsync();
        stored.PepperVersion.Should().BeNull();
        stored.RehashPending.Should().BeFalse();
        stored.LastUsedAt.Should().Be(now);
    }

    [Fact]
    public async Task RehashPendingApiKeys_updates_hash_and_clears_flag()
    {
        ConfigurePepperRotation();
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var token = "rmv_live_rehash_pending_key";
        var now = DateTimeOffset.Parse("2026-06-19T13:00:00Z");

        await SeedApiKeyAsync(
            fixture,
            user.Id,
            token,
            ApiKeyHashing.ComputeHashWithVersion(token, PreviousVersion),
            PreviousVersion);
        await using (var resolveDb = fixture.CreateContext())
        {
            var resolver = CreateResolver(resolveDb);
            var resolved = await resolver.ResolveAsync(
                CreateRequest(token),
                now,
                CancellationToken.None);
            resolved.UserId.Should().Be(user.Id);
        }

        await using var handlerDb = fixture.CreateContext();
        var handler = new RehashPendingApiKeysHandler(
            new ApiKeyRepository(handlerDb),
            new UnitOfWork(handlerDb));

        var result = await handler.HandleAsync(new RehashPendingApiKeysCommand());

        result.Examined.Should().Be(1);
        result.Cleared.Should().Be(1);
        var stored = await handlerDb.ApiKeys.SingleAsync();
        stored.KeyHash.Should().Be(ApiKeyHashing.ComputeHashWithVersion(token, CurrentVersion));
        stored.PepperVersion.Should().Be(CurrentVersion);
        stored.RehashPending.Should().BeFalse();
    }

    [Fact]
    public async Task GenerateApiKeyHandler_sets_pepper_version_and_rehash_pending_false()
    {
        ConfigureCurrentOnly("api-key-generate-current-pepper", 11);
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        await using var db = fixture.CreateContext();
        var handler = new GenerateApiKeyHandler(
            new ApiKeyRepository(db),
            new UnitOfWork(db));

        var generated = await handler.HandleAsync(
            new GenerateApiKeyCommand(user.Id, "Generated integration key", IsTest: true),
            CancellationToken.None);

        var stored = await db.ApiKeys.SingleAsync();
        stored.PepperVersion.Should().Be(11);
        stored.RehashPending.Should().BeFalse();
        stored.KeyHash.Should().Be(ApiKeyCredential.ComputeHashWithVersion(generated.Plaintext, 11));
    }

    [Fact]
    public void ApiKeyHashing_FixedTimeEquals_compares_hashes_without_plaintext_equality()
    {
        ConfigureCurrentOnly("api-key-compare-pepper", 13);
        var hash = ApiKeyHashing.ComputeHash("rmv_live_constant_time_key");
        var differentHash = ApiKeyHashing.ComputeHash("rmv_live_constant_time_other_key");

        ApiKeyHashing.FixedTimeEquals(hash, hash).Should().BeTrue();
        ApiKeyHashing.FixedTimeEquals(hash, differentHash).Should().BeFalse();
        ApiKeyHashing.FixedTimeEquals(hash, "not-a-hex-hash").Should().BeFalse();
    }

    private static async Task SeedApiKeyAsync(
        DbFixture fixture,
        Guid userId,
        string token,
        string keyHash,
        int? pepperVersion,
        bool rehashPending = false)
    {
        var now = DateTimeOffset.Parse("2026-06-19T11:00:00Z");
        await using var db = fixture.CreateContext();
        db.ApiKeys.Add(new ApiKey
        {
            UserId = userId,
            Name = "Pepper rotation key",
            KeyHash = keyHash,
            PepperVersion = pepperVersion,
            RehashPending = rehashPending,
            Last4 = token[^4..],
            CreatedAt = now,
            UpdatedAt = now,
        });
        await db.SaveChangesAsync();
    }

    private static void ConfigurePepperRotation()
    {
        ConfigureCurrentOnly(CurrentPepper, CurrentVersion);
        Environment.SetEnvironmentVariable("API_KEY_PREVIOUS_PEPPER", PreviousPepper);
        Environment.SetEnvironmentVariable("API_KEY_PREVIOUS_PEPPER_VERSION", PreviousVersion.ToString());
    }

    private static void ConfigureCurrentOnly(string currentPepper, int currentVersion)
    {
        Environment.SetEnvironmentVariable("API_KEY_PEPPER", currentPepper);
        Environment.SetEnvironmentVariable("API_KEY_PEPPER_VERSION", currentVersion.ToString());
        Environment.SetEnvironmentVariable("API_KEY_PREVIOUS_PEPPER", null);
        Environment.SetEnvironmentVariable("API_KEY_PREVIOUS_PEPPER_VERSION", null);
    }

    private static HttpRequest CreateRequest(string token)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = $"Bearer {token}";
        return context.Request;
    }

    private static ApiKeyAuthResolver CreateResolver(AppDbContext db) =>
        new(new ApiKeyRepository(db), new UnitOfWork(db));
}
