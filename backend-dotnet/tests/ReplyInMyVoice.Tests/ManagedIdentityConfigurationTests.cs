using FluentAssertions;
using Microsoft.Extensions.Configuration;
using ReplyInMyVoice.Infrastructure.Configuration;

namespace ReplyInMyVoice.Tests;

public sealed class ManagedIdentityConfigurationTests
{
    [Theory]
    [InlineData("true", true)]
    [InlineData("True", true)]
    [InlineData("false", false)]
    [InlineData("garbage", false)]
    [InlineData(null, false)]
    public void IsEnabled_requires_explicit_true(string? value, bool expected)
    {
        var values = new Dictionary<string, string?>();
        if (value is not null)
        {
            values["USE_MANAGED_IDENTITY"] = value;
        }

        var configuration = BuildConfiguration(values);

        ManagedIdentityConfiguration.IsEnabled(configuration).Should().Be(expected);
    }

    [Fact]
    public void ResolveServiceBusFullyQualifiedNamespace_normalizes_scheme_and_trailing_slash()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ServiceBus:fullyQualifiedNamespace"] = "  sb://ns.servicebus.windows.net/  ",
        });

        ManagedIdentityConfiguration.ResolveServiceBusFullyQualifiedNamespace(configuration)
            .Should()
            .Be("ns.servicebus.windows.net");
    }

    [Fact]
    public void ResolveServiceBusFullyQualifiedNamespace_prefers_servicebus_section_key_over_env_alias()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ServiceBus:fullyQualifiedNamespace"] = "primary.servicebus.windows.net",
            ["SERVICEBUS_FULLY_QUALIFIED_NAMESPACE"] = "fallback.servicebus.windows.net",
        });

        ManagedIdentityConfiguration.ResolveServiceBusFullyQualifiedNamespace(configuration)
            .Should()
            .Be("primary.servicebus.windows.net");
    }

    [Fact]
    public void ResolveServiceBusFullyQualifiedNamespace_returns_null_for_blank_values()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ServiceBus:fullyQualifiedNamespace"] = "  ",
            ["SERVICEBUS_FULLY_QUALIFIED_NAMESPACE"] = "\t",
        });

        ManagedIdentityConfiguration.ResolveServiceBusFullyQualifiedNamespace(configuration)
            .Should()
            .BeNull();
    }

    private static IConfiguration BuildConfiguration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
}
