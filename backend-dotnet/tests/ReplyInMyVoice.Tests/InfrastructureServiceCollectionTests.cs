using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReplyInMyVoice.Infrastructure;
using ReplyInMyVoice.Infrastructure.Data;
using ReplyInMyVoice.Infrastructure.Providers;

namespace ReplyInMyVoice.Tests;

public sealed class InfrastructureServiceCollectionTests
{
    [Fact]
    public void AddReplyInMyVoiceInfrastructure_uses_deterministic_rewrite_provider_without_live_keys()
    {
        var provider = BuildProvider([]);

        provider.GetRequiredService<IRewriteProvider>()
            .Should()
            .BeOfType<DeterministicRewriteProvider>();
    }

    [Fact]
    public void AddReplyInMyVoiceInfrastructure_uses_fact_reconstruct_provider_when_model_and_signal_keys_are_configured()
    {
        var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["OPENAI_BASE_URL"] = "https://api.deepseek.com",
            ["DEEPSEEK_API_KEY"] = "deepseek-test-key",
            ["OPENAI_MODEL_MID_WRITER"] = "deepseek-v4-pro",
            ["SAPLING_API_KEY"] = "sapling-test-key",
        });

        provider.GetRequiredService<IRewriteProvider>()
            .Should()
            .BeOfType<FactReconstructRewriteProvider>();
        provider.GetRequiredService<IRewriteModelClient>()
            .Should()
            .BeOfType<OpenAiCompatibleRewriteModelClient>();
        provider.GetRequiredService<IWritingSignalClient>()
            .Should()
            .BeOfType<SaplingWritingSignalClient>();
    }

    [Fact]
    public void AddReplyInMyVoiceInfrastructure_keeps_sapling_signal_even_when_pangram_is_requested()
    {
        // Guard: production stays on Sapling regardless of WRITING_SIGNAL_PROVIDER. Pangram is a
        // detection-first signal that fail-closes most send-ready rewrites, so it is an eval-only
        // comparison tool and must never become the live signal via an app-setting flip.
        var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["OPENAI_BASE_URL"] = "https://api.deepseek.com",
            ["DEEPSEEK_API_KEY"] = "deepseek-test-key",
            ["OPENAI_MODEL_MID_WRITER"] = "deepseek-v4-pro",
            ["SAPLING_API_KEY"] = "sapling-test-key",
            ["WRITING_SIGNAL_PROVIDER"] = "pangram",
            ["PANGRAM_API_KEY"] = "pangram-test-key",
        });

        provider.GetRequiredService<IWritingSignalClient>()
            .Should()
            .BeOfType<SaplingWritingSignalClient>();
    }

    [Fact]
    public void AddReplyInMyVoiceInfrastructure_enables_sql_server_retry_strategy_for_default_connection()
    {
        var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Server=localhost;Database=ReplyInMyVoiceTest;User Id=test;Password=test;TrustServerCertificate=True",
        });

        using var scope = provider.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<AppDbContext>>();
        using var db = new AppDbContext(options);

        var strategy = db.Database.CreateExecutionStrategy();

        strategy.RetriesOnFailure.Should().BeTrue();
        strategy.GetType().Name.Should().Be("SqlServerRetryingExecutionStrategy");
    }

    private static ServiceProvider BuildProvider(Dictionary<string, string?> values)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        var services = new ServiceCollection();
        services.AddReplyInMyVoiceInfrastructure(configuration);
        return services.BuildServiceProvider();
    }
}
