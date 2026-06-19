using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ReplyInMyVoice.Application.UseCases.ApiKey;
using ReplyInMyVoice.Functions.Auth;
using ReplyInMyVoice.Infrastructure;
using ReplyInMyVoice.Infrastructure.Data;
using ReplyInMyVoice.Infrastructure.Repositories;
using ReplyInMyVoice.Infrastructure.Services;

namespace ReplyInMyVoice.Tests.ApiKeyPepperRotation;

[Collection("ApiKeyPepper")]
public sealed class ApiKeyPepperRotationIntegrationTests
{
    [Fact]
    public async Task GenerateApiKey_sets_current_pepper_version()
    {
        using var env = ApiKeyAuthResolverRotationTests.UseTwoVersionEnvironment();
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        await using var db = fixture.CreateContext();
        var handler = new GenerateApiKeyHandler(
            new ApiKeyRepository(db),
            new UnitOfWork(db));

        var generated = await handler.HandleAsync(
            new GenerateApiKeyCommand(user.Id, "Current version key"));

        var stored = await db.ApiKeys.SingleAsync();
        stored.PepperVersion.Should().Be(2);
        stored.RehashPending.Should().BeFalse();
        stored.KeyHash.Should().Be(ApiKeyHashing.ComputeHashWithVersion(generated.Plaintext, 2));
    }

    [Fact]
    public async Task Existing_key_resolves_then_rehash_flag_set()
    {
        using var env = ApiKeyAuthResolverRotationTests.UseTwoVersionEnvironment();
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var token = "rmv_live_existing_key_rehash_token";
        var originalHash = await ApiKeyAuthResolverRotationTests.SeedApiKeyAsync(
            fixture,
            user.Id,
            token,
            pepperVersion: 1);
        await using var db = fixture.CreateContext();
        var request = ApiKeyAuthResolverRotationTests.CreateRequest(token);

        var result = await ApiKeyAuthResolver.ResolveAsync(
            request,
            db,
            DateTimeOffset.Parse("2026-06-19T01:20:00Z"),
            CancellationToken.None);

        result.UserId.Should().Be(user.Id);
        result.NeedsRehash.Should().BeTrue();
        var pending = await db.ApiKeys.SingleAsync();
        var pendingRowVersion = pending.RowVersion;
        pending.RehashPending.Should().BeTrue();
        pending.KeyHash.Should().Be(originalHash);

        var updated = await ApiKeyAuthResolver.RehashIfNeededAsync(
            request,
            db,
            result,
            CancellationToken.None);

        updated.Should().BeTrue();
        var stored = await db.ApiKeys.SingleAsync();
        stored.KeyHash.Should().Be(ApiKeyHashing.ComputeHashWithVersion(token, 2));
        stored.PepperVersion.Should().Be(2);
        stored.RehashPending.Should().BeFalse();
        stored.RowVersion.Should().NotBe(pendingRowVersion);
    }

    [Fact]
    public void AddReplyInMyVoiceInfrastructure_requires_base_pepper_when_versioned_pepper_is_set_in_production()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DATABASE_URL"] = "Server=localhost;Database=ReplyInMyVoiceTest;User Id=test;Password=test;TrustServerCertificate=True",
                ["STRIPE_SECRET_KEY"] = "stripe-test-key",
                ["STRIPE_WEBHOOK_SECRET"] = "stripe-webhook-test-key",
                ["OPENAI_BASE_URL"] = "https://api.deepseek.com",
                ["DEEPSEEK_API_KEY"] = "deepseek-test-key",
                ["SAPLING_API_KEY"] = "sapling-test-key",
                ["API_KEY_PEPPER_V2"] = ApiKeyAuthResolverRotationTests.TestValue('c'),
            })
            .Build();
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();

        var act = () => services.AddReplyInMyVoiceInfrastructure(configuration, "Production");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*API_KEY_PEPPER*");
    }
}
