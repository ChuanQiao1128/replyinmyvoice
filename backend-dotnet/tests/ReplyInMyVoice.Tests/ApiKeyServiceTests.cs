using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Infrastructure.Services;

namespace ReplyInMyVoice.Tests;

public sealed class ApiKeyServiceTests
{
    private const string TestPepper = "api-key-service-test-pepper";

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
}
