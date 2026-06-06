using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Functions.Auth;
using ReplyInMyVoice.Infrastructure.Services;

namespace ReplyInMyVoice.Tests;

[Collection("ApiKeyPepper")]
public sealed class ApiKeyServiceTests
{
    private const string TestPepper = "api-key-test-pepper";

    [Fact]
    public async Task GenerateAsync_returns_plaintext_once_and_stores_hash_and_last4()
    {
        Environment.SetEnvironmentVariable("API_KEY_PEPPER", TestPepper);
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var service = new ApiKeyService(fixture.CreateContext);

        var result = await service.GenerateAsync(
            user.Id,
            "Primary integration key",
            CancellationToken.None);

        result.Plaintext.Should().StartWith("rmv_live_");
        result.Id.Should().NotBeEmpty();

        await using var db = fixture.CreateContext();
        var stored = await db.ApiKeys.SingleAsync(x => x.Id == result.Id);
        stored.UserId.Should().Be(user.Id);
        stored.Name.Should().Be("Primary integration key");
        stored.KeyHash.Should().NotBe(result.Plaintext);
        stored.KeyHash.Should().Be(ApiKeyService.ComputeHash(result.Plaintext));
        stored.Last4.Should().Be(result.Plaintext[^4..]);
        stored.IsTest.Should().BeFalse();
    }

    [Fact]
    public async Task GenerateAsync_can_create_test_keys_and_list_marks_them_as_test()
    {
        Environment.SetEnvironmentVariable("API_KEY_PEPPER", TestPepper);
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var service = new ApiKeyService(fixture.CreateContext);

        var result = await service.GenerateAsync(
            user.Id,
            "Sandbox client",
            CancellationToken.None,
            isTest: true);

        result.Plaintext.Should().StartWith("rmv_test_");

        await using var db = fixture.CreateContext();
        var stored = await db.ApiKeys.SingleAsync(x => x.Id == result.Id);
        stored.IsTest.Should().BeTrue();
        stored.KeyHash.Should().Be(ApiKeyService.ComputeHash(result.Plaintext));
        stored.Last4.Should().Be(result.Plaintext[^4..]);

        var summaries = await service.ListAsync(user.Id, CancellationToken.None);

        var summary = summaries.Should().ContainSingle().Subject;
        summary.IsTest.Should().BeTrue();
        summary.MaskedKey.Should().StartWith("rmv_test_");
        summary.MaskedKey.Should().EndWith(stored.Last4!);
    }

    [Fact]
    public void ComputeHash_is_deterministic_for_the_same_input()
    {
        Environment.SetEnvironmentVariable("API_KEY_PEPPER", TestPepper);

        var first = ApiKeyService.ComputeHash("rmv_live_sample_key");
        var second = ApiKeyService.ComputeHash("rmv_live_sample_key");

        second.Should().Be(first);
        first.Should().MatchRegex("^[0-9a-f]{64}$");
    }

    [Theory]
    [InlineData("http://example.com/rewrite")]
    [InlineData("https://127.0.0.1/rewrite")]
    [InlineData("https://169.254.169.254/latest/meta-data")]
    [InlineData("https://10.0.0.5/rewrite")]
    [InlineData("https://localhost/rewrite")]
    public void TryNormalizeWebhookUrl_rejects_non_https_and_non_public_targets(string value)
    {
        var valid = ApiKeyService.TryNormalizeWebhookUrl(value, out var normalizedUrl);

        valid.Should().BeFalse();
        normalizedUrl.Should().BeEmpty();
    }

    [Fact]
    public void TryNormalizeWebhookUrl_accepts_public_https_url()
    {
        var valid = ApiKeyService.TryNormalizeWebhookUrl(
            "https://example.com/rewrite",
            out var normalizedUrl,
            _ => [IPAddress.Parse("93.184.216.34")]);

        valid.Should().BeTrue();
        normalizedUrl.Should().Be("https://example.com/rewrite");
    }

    [Fact]
    public async Task ListAsync_returns_masked_summaries_without_plaintext_or_hash()
    {
        Environment.SetEnvironmentVariable("API_KEY_PEPPER", TestPepper);
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var service = new ApiKeyService(fixture.CreateContext);
        var generated = await service.GenerateAsync(user.Id, "Mobile client", CancellationToken.None);

        await using var db = fixture.CreateContext();
        var stored = await db.ApiKeys.SingleAsync(x => x.Id == generated.Id);

        var summaries = await service.ListAsync(user.Id, CancellationToken.None);

        var summary = summaries.Should().ContainSingle().Subject;
        summary.Id.Should().Be(generated.Id);
        summary.Name.Should().Be("Mobile client");
        summary.MaskedKey.Should().StartWith("rmv_live_");
        summary.MaskedKey.Should().EndWith(stored.Last4!);
        summary.IsTest.Should().BeFalse();
        summary.ToString().Should().NotContain(generated.Plaintext);
        summary.ToString().Should().NotContain(stored.KeyHash);
    }

    [Fact]
    public async Task RevokeAsync_sets_revoked_at_for_owner_and_returns_false_for_non_owner()
    {
        Environment.SetEnvironmentVariable("API_KEY_PEPPER", TestPepper);
        await using var fixture = await DbFixture.CreateAsync();
        var owner = await fixture.CreateUserAsync();
        var nonOwner = await fixture.CreateUserAsync();
        var service = new ApiKeyService(fixture.CreateContext);
        var generated = await service.GenerateAsync(owner.Id, "Server key", CancellationToken.None);

        var nonOwnerResult = await service.RevokeAsync(nonOwner.Id, generated.Id, CancellationToken.None);

        nonOwnerResult.Should().BeFalse();
        await using (var unchangedDb = fixture.CreateContext())
        {
            var unchanged = await unchangedDb.ApiKeys.SingleAsync(x => x.Id == generated.Id);
            unchanged.RevokedAt.Should().BeNull();
        }

        var ownerResult = await service.RevokeAsync(owner.Id, generated.Id, CancellationToken.None);

        ownerResult.Should().BeTrue();
        await using var db = fixture.CreateContext();
        var stored = await db.ApiKeys.SingleAsync(x => x.Id == generated.Id);
        stored.RevokedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task RotateAsync_creates_new_active_key_and_revokes_old_key_for_owner()
    {
        Environment.SetEnvironmentVariable("API_KEY_PEPPER", TestPepper);
        await using var fixture = await DbFixture.CreateAsync();
        var owner = await fixture.CreateUserAsync();
        var nonOwner = await fixture.CreateUserAsync();
        var service = new ApiKeyService(fixture.CreateContext);
        var generated = await service.GenerateAsync(owner.Id, "Server key", CancellationToken.None);

        var nonOwnerResult = await service.RotateAsync(nonOwner.Id, generated.Id, CancellationToken.None);

        nonOwnerResult.Should().BeNull();
        await using (var unchangedDb = fixture.CreateContext())
        {
            var unchanged = await unchangedDb.ApiKeys.SingleAsync(x => x.Id == generated.Id);
            unchanged.RevokedAt.Should().BeNull();
        }

        var rotated = await service.RotateAsync(owner.Id, generated.Id, CancellationToken.None);

        rotated.Should().NotBeNull();
        rotated!.Name.Should().Be("Server key");
        rotated.Id.Should().NotBe(generated.Id);
        rotated.Plaintext.Should().StartWith("rmv_live_");
        rotated.Plaintext.Should().NotBe(generated.Plaintext);

        await using (var db = fixture.CreateContext())
        {
            var oldKey = await db.ApiKeys.SingleAsync(x => x.Id == generated.Id);
            var newKey = await db.ApiKeys.SingleAsync(x => x.Id == rotated.Id);

            oldKey.RevokedAt.Should().NotBeNull();
            newKey.RevokedAt.Should().BeNull();
            newKey.Name.Should().Be(oldKey.Name);
            newKey.KeyHash.Should().Be(ApiKeyService.ComputeHash(rotated.Plaintext));
            newKey.KeyHash.Should().NotBe(rotated.Plaintext);
            newKey.Last4.Should().Be(rotated.Plaintext[^4..]);
        }

        await using (var authDb = fixture.CreateContext())
        {
            var oldRequest = CreateBearerRequest(generated.Plaintext);
            var newRequest = CreateBearerRequest(rotated.Plaintext);
            var now = DateTimeOffset.UtcNow;

            var oldUserId = await ApiKeyAuthResolver.ResolveUserIdAsync(
                oldRequest,
                authDb,
                now,
                CancellationToken.None);
            var newUserId = await ApiKeyAuthResolver.ResolveUserIdAsync(
                newRequest,
                authDb,
                now,
                CancellationToken.None);

            oldUserId.Should().BeNull();
            newUserId.Should().Be(owner.Id);
        }
    }

    [Fact]
    public async Task ListAsync_includes_per_key_last_30_day_usage_for_owned_keys()
    {
        Environment.SetEnvironmentVariable("API_KEY_PEPPER", TestPepper);
        await using var fixture = await DbFixture.CreateAsync();
        var owner = await fixture.CreateUserAsync();
        var other = await fixture.CreateUserAsync();
        var service = new ApiKeyService(fixture.CreateContext);
        var ownerPrimary = await service.GenerateAsync(owner.Id, "Primary", CancellationToken.None);
        var ownerSecondary = await service.GenerateAsync(owner.Id, "Secondary", CancellationToken.None);
        var otherKey = await service.GenerateAsync(other.Id, "Other", CancellationToken.None);
        var now = DateTimeOffset.UtcNow;

        await using (var db = fixture.CreateContext())
        {
            db.ApiKeyUsages.AddRange(
                Usage(ownerPrimary.Id, "owner-primary-ok", 200, now.AddDays(-1)),
                Usage(ownerPrimary.Id, "owner-primary-accepted", 202, now.AddDays(-2)),
                Usage(ownerPrimary.Id, "owner-primary-failed", 500, now.AddDays(-3)),
                Usage(ownerPrimary.Id, "owner-primary-old", 200, now.AddDays(-31)),
                Usage(ownerSecondary.Id, "owner-secondary-ok", 200, now.AddDays(-4)),
                Usage(otherKey.Id, "other-ok", 200, now.AddDays(-1)));
            await db.SaveChangesAsync();
        }

        var summaries = await service.ListAsync(owner.Id, CancellationToken.None);

        summaries.Should().HaveCount(2);
        var primary = summaries.Single(x => x.Id == ownerPrimary.Id);
        var secondary = summaries.Single(x => x.Id == ownerSecondary.Id);

        primary.Last30dUsage.Should().Be(new ApiUsageCount(3, 2, 1));
        secondary.Last30dUsage.Should().Be(new ApiUsageCount(1, 1, 0));
        summaries.Select(x => x.Id).Should().NotContain(otherKey.Id);
    }

    private static HttpRequest CreateBearerRequest(string token)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = $"Bearer {token}";
        return context.Request;
    }

    private static ApiKeyUsage Usage(
        Guid apiKeyId,
        string requestId,
        int statusCode,
        DateTimeOffset createdAt) =>
        new()
        {
            ApiKeyId = apiKeyId,
            RequestId = requestId,
            Endpoint = "v1/rewrite",
            StatusCode = statusCode,
            CreatedAt = createdAt,
        };
}
