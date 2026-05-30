using System.Reflection;
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
    public void AddReplyInMyVoiceInfrastructure_bounds_fact_reconstruct_provider_by_default()
    {
        var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["OPENAI_BASE_URL"] = "https://api.deepseek.com",
            ["DEEPSEEK_API_KEY"] = "deepseek-test-key",
            ["OPENAI_MODEL_MID_WRITER"] = "deepseek-v4-pro",
            ["SAPLING_API_KEY"] = "sapling-test-key",
        });

        var options = ReadFactReconstructOptions(provider.GetRequiredService<IRewriteProvider>());

        options.TotalTimeBudget.Should().Be(TimeSpan.FromSeconds(180));
    }

    [Fact]
    public void AddReplyInMyVoiceInfrastructure_allows_explicit_unbounded_fact_reconstruct_budget()
    {
        var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["OPENAI_BASE_URL"] = "https://api.deepseek.com",
            ["DEEPSEEK_API_KEY"] = "deepseek-test-key",
            ["OPENAI_MODEL_MID_WRITER"] = "deepseek-v4-pro",
            ["SAPLING_API_KEY"] = "sapling-test-key",
            ["TOTAL_REWRITE_BUDGET_SEC"] = "0",
        });

        var options = ReadFactReconstructOptions(provider.GetRequiredService<IRewriteProvider>());

        options.TotalTimeBudget.Should().Be(TimeSpan.Zero);
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

    [Fact]
    public void Worker_program_gates_in_process_service_bus_consumer_by_default()
    {
        var program = File.ReadAllText(WorkerProgramPath());
        var flagIndex = program.IndexOf("ENABLE_INPROC_REWRITE_WORKER", StringComparison.Ordinal);

        flagIndex.Should().BeGreaterThanOrEqualTo(0);
        program.Should().Contain("bool.TryParse(builder.Configuration[\"ENABLE_INPROC_REWRITE_WORKER\"], out var enableInProcRewriteWorker)");
        program.Should().Contain("&& enableInProcRewriteWorker");
        program.Should().Contain("AddHostedService<OutboxDispatcherWorker>()");
        program.Should().Contain("AddHostedService<ExpiredReservationCleanupWorker>()");
        program[..flagIndex].Should().NotContain("AddHostedService<ServiceBusRewriteWorker>()");
        program[flagIndex..].Should().Contain("AddHostedService<ServiceBusRewriteWorker>()");
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

    private static FactReconstructRewriteOptions ReadFactReconstructOptions(IRewriteProvider provider)
    {
        var optionsField = typeof(FactReconstructRewriteProvider).GetField(
            "_options",
            BindingFlags.Instance | BindingFlags.NonPublic);

        optionsField.Should().NotBeNull();
        return (FactReconstructRewriteOptions)optionsField!.GetValue(provider)!;
    }

    private static string WorkerProgramPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(
                dir.FullName,
                "backend-dotnet",
                "src",
                "ReplyInMyVoice.Worker",
                "Program.cs");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not locate backend-dotnet/src/ReplyInMyVoice.Worker/Program.cs from the test base directory.");
    }
}
