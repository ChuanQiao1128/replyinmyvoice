using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Functions.Auth;
using ReplyInMyVoice.Infrastructure.Data;
using ReplyInMyVoice.Infrastructure.Services;

namespace ReplyInMyVoice.Tests.ApiKeyPepperRotation;

[Collection("ApiKeyPepper")]
public sealed class ApiKeyAuthResolverRotationTests
{
    [Fact]
    public async Task ResolveAsync_validates_current_pepper_first()
    {
        using var env = UseTwoVersionEnvironment();
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var token = "rmv_live_current_pepper_token";
        var now = DateTimeOffset.Parse("2026-06-19T01:00:00Z");
        await SeedApiKeyAsync(fixture, user.Id, token, pepperVersion: 2);
        await using var db = fixture.CreateContext();

        var result = await ApiKeyAuthResolver.ResolveAsync(
            CreateRequest(token),
            db,
            now,
            CancellationToken.None);

        result.UserId.Should().Be(user.Id);
        result.ApiKeyId.Should().NotBeNull();
        result.NeedsRehash.Should().BeFalse();
        var stored = await db.ApiKeys.SingleAsync();
        stored.RehashPending.Should().BeFalse();
        stored.PepperVersion.Should().Be(2);
        stored.LastUsedAt.Should().Be(now);
    }

    [Fact]
    public async Task ResolveAsync_validates_previous_pepper_on_miss()
    {
        using var env = UseTwoVersionEnvironment();
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var token = "rmv_live_previous_pepper_token";
        var now = DateTimeOffset.Parse("2026-06-19T01:05:00Z");
        var originalHash = await SeedApiKeyAsync(fixture, user.Id, token, pepperVersion: 1);
        await using var db = fixture.CreateContext();

        var result = await ApiKeyAuthResolver.ResolveAsync(
            CreateRequest(token),
            db,
            now,
            CancellationToken.None);

        result.UserId.Should().Be(user.Id);
        result.ApiKeyId.Should().NotBeNull();
        result.NeedsRehash.Should().BeTrue();
        var stored = await db.ApiKeys.SingleAsync();
        stored.RehashPending.Should().BeTrue();
        stored.PepperVersion.Should().Be(1);
        stored.KeyHash.Should().Be(originalHash);
        stored.LastUsedAt.Should().Be(now);
    }

    [Fact]
    public async Task ResolveAsync_returns_null_if_no_pepper_matches()
    {
        using var env = UseTwoVersionEnvironment();
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        await SeedApiKeyAsync(fixture, user.Id, "rmv_live_known_token", pepperVersion: 1);
        await using var db = fixture.CreateContext();

        var result = await ApiKeyAuthResolver.ResolveAsync(
            CreateRequest("rmv_live_unknown_token"),
            db,
            DateTimeOffset.Parse("2026-06-19T01:10:00Z"),
            CancellationToken.None);

        result.UserId.Should().BeNull();
        result.ApiKeyId.Should().BeNull();
        result.NeedsRehash.Should().BeFalse();
    }

    [Fact]
    public void ResolveAsync_preserves_constant_time_comparison()
    {
        var sourcePath = Path.Combine(
            FindRepoRoot(),
            "backend-dotnet",
            "src",
            "ReplyInMyVoice.Functions",
            "Auth",
            "ApiKeyAuthResolver.cs");
        var source = File.ReadAllText(sourcePath);

        source.Should().Contain("CryptographicOperations.FixedTimeEquals");
        source.Should().NotContain("KeyHash ==");
    }

    [Fact]
    public async Task ResolveAsync_does_not_mutate_hash_on_old_pepper_auth()
    {
        using var env = UseTwoVersionEnvironment();
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var token = "rmv_live_old_hash_stays_put_token";
        var originalHash = await SeedApiKeyAsync(fixture, user.Id, token, pepperVersion: 1);
        await using var db = fixture.CreateContext();

        var result = await ApiKeyAuthResolver.ResolveAsync(
            CreateRequest(token),
            db,
            DateTimeOffset.Parse("2026-06-19T01:15:00Z"),
            CancellationToken.None);

        result.NeedsRehash.Should().BeTrue();
        var stored = await db.ApiKeys.SingleAsync();
        stored.KeyHash.Should().Be(originalHash);
        stored.KeyHash.Should().NotBe(ApiKeyHashing.ComputeHashWithVersion(token, 2));
        stored.RehashPending.Should().BeTrue();
    }

    internal static PepperEnvironmentScope UseTwoVersionEnvironment() =>
        new(new Dictionary<string, string?>
        {
            ["API_KEY_PEPPER"] = TestValue('a'),
            ["API_KEY_PEPPER_V2"] = TestValue('b'),
        });

    internal static string TestValue(char value) => new(value, 40);

    internal static async Task<string> SeedApiKeyAsync(
        DbFixture fixture,
        Guid userId,
        string token,
        int pepperVersion)
    {
        var now = DateTimeOffset.Parse("2026-06-19T00:00:00Z");
        var hash = ApiKeyHashing.ComputeHashWithVersion(token, pepperVersion);
        await using var db = fixture.CreateContext();
        db.ApiKeys.Add(new ApiKey
        {
            UserId = userId,
            Name = "Rotation test key",
            KeyHash = hash,
            PepperVersion = pepperVersion,
            Last4 = token[^4..],
            CreatedAt = now,
            UpdatedAt = now,
        });
        await db.SaveChangesAsync();
        return hash;
    }

    internal static HttpRequest CreateRequest(string token)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = $"Bearer {token}";
        return context.Request;
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (directory.GetDirectories("backend-dotnet").Any())
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
