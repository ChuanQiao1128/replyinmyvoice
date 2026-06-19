using System.Collections.Concurrent;
using System.Diagnostics;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ReplyInMyVoice.Infrastructure;
using ReplyInMyVoice.Infrastructure.Observability;

namespace ReplyInMyVoice.Tests;

public sealed class DistributedTracingActivitySourceTests
{
    [Fact]
    public void AddReplyInMyVoiceInfrastructure_registers_distributed_tracing_activity_source()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OTEL_ENABLED"] = "true",
            })
            .Build();
        var services = new ServiceCollection();

        services.AddReplyInMyVoiceInfrastructure(configuration, "Testing");
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<DistributedTracingActivitySource>()
            .ActivitySource
            .Name
            .Should()
            .Be(DistributedTracingActivitySource.SourceName);
        provider.GetRequiredService<ActivitySource>()
            .Should()
            .BeSameAs(DistributedTracingActivitySource.Source);
    }

    [Fact]
    public void DistributedTracingOptions_defaults_to_enabled_only_for_production()
    {
        DistributedTracingOptions.IsEnabled(new ConfigurationBuilder().Build(), "Production")
            .Should()
            .BeTrue();
        DistributedTracingOptions.IsEnabled(new ConfigurationBuilder().Build(), "Testing")
            .Should()
            .BeFalse();

        var disabledProduction = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OTEL_ENABLED"] = "false",
            })
            .Build();
        DistributedTracingOptions.IsEnabled(disabledProduction, "Production")
            .Should()
            .BeFalse();
    }

    [Fact]
    public async Task Api_health_request_emits_activity_when_otel_enabled()
    {
        var stopped = new ConcurrentBag<Activity>();
        using var listener = CreateListener(stopped);
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["OTEL_ENABLED"] = "true",
                    });
                });
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<DistributedTracingSettings>();
                    services.AddSingleton(new DistributedTracingSettings(Enabled: true));
                });
            });
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false,
        });

        var response = await client.GetAsync("/health");

        response.EnsureSuccessStatusCode();
        var activity = stopped.Should()
            .ContainSingle(x => x.OperationName == "Api.HttpRequest")
            .Subject;
        activity.Kind.Should().Be(ActivityKind.Server);
        activity.GetTagItem("CorrelationId").Should().NotBeNull();
    }

    private static ActivityListener CreateListener(ConcurrentBag<Activity> stopped)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == DistributedTracingActivitySource.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = stopped.Add,
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }
}
