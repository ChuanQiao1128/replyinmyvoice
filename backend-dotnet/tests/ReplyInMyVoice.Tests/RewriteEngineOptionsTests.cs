using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Infrastructure;
using ReplyInMyVoice.Infrastructure.Configuration;
using ReplyInMyVoice.Infrastructure.Providers;

namespace ReplyInMyVoice.Tests;

public sealed class RewriteEngineOptionsTests
{
    [Fact]
    public void Options_resolve_with_defaults_and_provider_di_still_builds()
    {
        using var provider = BuildProvider([]);

        var options = provider.GetRequiredService<IOptions<RewriteEngineOptions>>().Value;

        options.ModelBaseUrl.Should().Be("https://api.openai.com/v1");
        options.Model.Should().Be("gpt-4o-mini");
        options.ModelTimeoutSeconds.Should().Be(60);
        options.SignalTimeoutSeconds.Should().Be(10);
        options.AiSignalTarget.Should().Be(20);
        options.MaxAttempts.Should().Be(10);
        options.TotalBudgetSeconds.Should().Be(180);
        provider.GetRequiredService<IRewriteProvider>()
            .Should()
            .BeOfType<DeterministicRewriteProvider>();
    }

    [Fact]
    public void Options_bind_existing_environment_keys_for_rewrite_provider()
    {
        var values = CompleteProductionConfiguration();
        values["OPENAI_BASE_URL"] = "https://api.deepseek.com/v1";
        values["OPENAI_MODEL_MID_WRITER"] = "deepseek-v4-pro";
        values["OPENAI_TIMEOUT_SEC"] = "45";
        values["WRITING_SIGNAL_TIMEOUT_SEC"] = "8";
        values["AI_SIGNAL_TARGET"] = "15";
        values["REWRITE_MAX_ATTEMPTS"] = "7";
        values["TOTAL_REWRITE_BUDGET_SEC"] = "120";

        using var provider = BuildProvider(values, environmentName: "Production");

        var options = provider.GetRequiredService<IOptions<RewriteEngineOptions>>().Value;
        options.ModelBaseUrl.Should().Be("https://api.deepseek.com/v1");
        options.Model.Should().Be("deepseek-v4-pro");
        options.ModelTimeoutSeconds.Should().Be(45);
        options.SignalTimeoutSeconds.Should().Be(8);
        options.AiSignalTarget.Should().Be(15);
        options.MaxAttempts.Should().Be(7);
        options.TotalBudgetSeconds.Should().Be(120);
        provider.GetRequiredService<IRewriteProvider>()
            .Should()
            .BeOfType<FactReconstructRewriteProvider>();
    }

    [Theory]
    [InlineData("OPENAI_TIMEOUT_SEC", "-1")]
    [InlineData("REWRITE_MAX_ATTEMPTS", "abc")]
    public void Options_fail_in_non_development_environments_when_rewrite_setting_is_invalid(
        string key,
        string value)
    {
        var values = CompleteProductionConfiguration();
        values[key] = value;

        using var provider = BuildProvider(values, environmentName: "Production");

        var act = () => provider.GetRequiredService<IOptions<RewriteEngineOptions>>().Value;

        var exception = act.Should().Throw<OptionsValidationException>().Which;
        exception.Message.Should().Contain(key);
        exception.Message.Should().NotContain(value);
        exception.Message.Should().NotContain("deepseek-test-key");
        exception.Message.Should().NotContain("sapling-test-key");
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Testing")]
    public void Options_do_not_hard_fail_in_development_or_testing_when_rewrite_setting_is_invalid(
        string environmentName)
    {
        using var provider = BuildProvider(
            new Dictionary<string, string?>
            {
                ["OPENAI_TIMEOUT_SEC"] = "-1",
            },
            environmentName);

        var act = () => provider.GetRequiredService<IOptions<RewriteEngineOptions>>().Value;

        act.Should().NotThrow();
    }

    [Fact]
    public void KeyVault_guard_returns_builder_unchanged_when_vault_uri_is_unset()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection([])
            .Build();
        var builder = new ConfigurationBuilder();

        var returned = builder.AddReplyInMyVoiceKeyVault(configuration);

        returned.Should().BeSameAs(builder);
        KeyVaultConfigurationExtensions.ShouldAttach(configuration).Should().BeFalse();
    }

    [Fact]
    public void KeyVault_guard_requires_managed_identity_and_absolute_https_vault_uri()
    {
        var disabledConfiguration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["KEY_VAULT_URI"] = "https://rimv-test.vault.azure.net/",
            ["USE_MANAGED_IDENTITY"] = "false",
        });
        var invalidConfiguration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["KEY_VAULT_URI"] = "http://rimv-test.vault.azure.net/",
            ["USE_MANAGED_IDENTITY"] = "true",
        });
        var enabledConfiguration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["AZURE_KEY_VAULT_URI"] = "https://rimv-test.vault.azure.net/",
            ["USE_MANAGED_IDENTITY"] = "true",
        });

        KeyVaultConfigurationExtensions.ShouldAttach(disabledConfiguration).Should().BeFalse();
        KeyVaultConfigurationExtensions.ShouldAttach(invalidConfiguration).Should().BeFalse();
        KeyVaultConfigurationExtensions.ShouldAttach(enabledConfiguration).Should().BeTrue();
        KeyVaultConfigurationExtensions.ResolveVaultUri(enabledConfiguration)
            .Should()
            .Be(new Uri("https://rimv-test.vault.azure.net/"));
    }

    private static ServiceProvider BuildProvider(
        Dictionary<string, string?> values,
        string environmentName = "Testing")
    {
        var configuration = BuildConfiguration(values);
        var services = new ServiceCollection();
        services.AddReplyInMyVoiceInfrastructure(configuration, environmentName);
        return services.BuildServiceProvider();
    }

    private static IConfigurationRoot BuildConfiguration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

    private static Dictionary<string, string?> CompleteProductionConfiguration() => new()
    {
        ["DATABASE_URL"] = "Server=localhost;Database=ReplyInMyVoiceTest;User Id=test;Password=test;TrustServerCertificate=True",
        ["STRIPE_SECRET_KEY"] = "stripe-test-key",
        ["STRIPE_WEBHOOK_SECRET"] = "stripe-webhook-test-key",
        ["OPENAI_BASE_URL"] = "https://api.deepseek.com",
        ["DEEPSEEK_API_KEY"] = "deepseek-test-key",
        ["SAPLING_API_KEY"] = "sapling-test-key",
    };
}
