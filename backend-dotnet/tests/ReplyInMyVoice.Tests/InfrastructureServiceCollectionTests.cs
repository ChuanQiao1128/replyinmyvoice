using System.Net.Http;
using System.Reflection;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using ReplyInMyVoice.Application.Common;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.UseCases.Account;
using ReplyInMyVoice.Application.UseCases.BillingSupport;
using ReplyInMyVoice.Application.UseCases.Quota;
using ReplyInMyVoice.Application.UseCases.Rewrite;
using ReplyInMyVoice.Application.UseCases.RewriteJob;
using ReplyInMyVoice.Application.UseCases.StripeEvent;
using ReplyInMyVoice.Application.UseCases.StripeReconciliation;
using ReplyInMyVoice.Infrastructure;
using ReplyInMyVoice.Infrastructure.Data;
using ReplyInMyVoice.Infrastructure.Providers;
using ReplyInMyVoice.Infrastructure.Resilience;
using ReplyInMyVoice.Infrastructure.Services;
using AppStripePaymentReconciliationClient = ReplyInMyVoice.Application.Abstractions.IStripePaymentReconciliationClient;
using AppStripeReconciliationAlerter = ReplyInMyVoice.Application.Abstractions.IStripeReconciliationAlerter;

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
    public void AddReplyInMyVoiceInfrastructure_registers_application_repositories()
    {
        var provider = BuildProvider([]);

        using var scope = provider.CreateScope();
        var scopedProvider = scope.ServiceProvider;

        scopedProvider.GetRequiredService<IAppUserRepository>().Should().NotBeNull();
        scopedProvider.GetRequiredService<IUsagePeriodRepository>().Should().NotBeNull();
        scopedProvider.GetRequiredService<IRewriteAttemptRepository>().Should().NotBeNull();
        scopedProvider.GetRequiredService<IUsageReservationRepository>().Should().NotBeNull();
        scopedProvider.GetRequiredService<IRewriteCreditRepository>().Should().NotBeNull();
        scopedProvider.GetRequiredService<IOutboxMessageRepository>().Should().NotBeNull();
        scopedProvider.GetRequiredService<IPromoCodeRepository>().Should().NotBeNull();
        scopedProvider.GetRequiredService<IPromoCodeRedemptionRepository>().Should().NotBeNull();
        scopedProvider.GetRequiredService<IStripeInvoiceRepository>().Should().NotBeNull();
        scopedProvider.GetRequiredService<IBillingSupportRepository>().Should().NotBeNull();
        scopedProvider.GetRequiredService<IBillingSupportRequestRepository>().Should().NotBeNull();
        scopedProvider.GetRequiredService<IPaymentGrantRepository>().Should().NotBeNull();
        scopedProvider.GetRequiredService<IAccountUsagePlanProvider>().Should().NotBeNull();
        scopedProvider.GetRequiredService<IUnitOfWork>().Should().NotBeNull();
        scopedProvider.GetRequiredService<AppStripePaymentReconciliationClient>().Should().NotBeNull();
        scopedProvider.GetRequiredService<AppStripeReconciliationAlerter>().Should().NotBeNull();
        scopedProvider.GetRequiredService<IRewriteEngineClient>().Should().NotBeNull();
        scopedProvider.GetRequiredService<IRewriteCostLogger>().Should().NotBeNull();
        scopedProvider.GetRequiredService<GetOrCreateUserHandler>().Should().NotBeNull();
        scopedProvider.GetRequiredService<FindUserHandler>().Should().NotBeNull();
        scopedProvider.GetRequiredService<GetAccountSummaryHandler>().Should().NotBeNull();
        scopedProvider.GetRequiredService<GetPurchaseHistoryHandler>().Should().NotBeNull();
        scopedProvider.GetRequiredService<HasPaidApiEntitlementHandler>().Should().NotBeNull();
        scopedProvider.GetRequiredService<GetBillingHistoryHandler>().Should().NotBeNull();
        scopedProvider.GetRequiredService<CreateBillingSupportRequestHandler>().Should().NotBeNull();
        scopedProvider.GetRequiredService<GetBillingSupportRequestsHandler>().Should().NotBeNull();
        scopedProvider.GetRequiredService<DeleteAccountHandler>().Should().NotBeNull();
        scopedProvider.GetRequiredService<ReserveQuotaHandler>().Should().NotBeNull();
        scopedProvider.GetRequiredService<FinalizeQuotaSuccessHandler>().Should().NotBeNull();
        scopedProvider.GetRequiredService<MarkQuotaProcessingHandler>().Should().NotBeNull();
        scopedProvider.GetRequiredService<ReleaseQuotaHandler>().Should().NotBeNull();
        scopedProvider.GetRequiredService<ReleaseExpiredReservationsHandler>().Should().NotBeNull();
        scopedProvider.GetRequiredService<CreateRewriteAttemptHandler>().Should().NotBeNull();
        scopedProvider.GetRequiredService<GetRewriteAttemptHandler>().Should().NotBeNull();
        scopedProvider.GetRequiredService<ReconcileStripeHandler>().Should().NotBeNull();
        scopedProvider.GetRequiredService<ProcessRewriteJobHandler>().Should().NotBeNull();
        scopedProvider.GetRequiredService<IngestStripeWebhookHandler>().Should().NotBeNull();
        scopedProvider.GetRequiredService<ProcessPendingStripeEventsHandler>().Should().NotBeNull();
        scopedProvider.GetRequiredService<StripeEventPayloadSynchronizer>().Should().NotBeNull();
        provider.GetRequiredService<StripeEventProcessingOptions>().Should().NotBeNull();
    }

    [Fact]
    public void AddReplyInMyVoiceInfrastructure_registers_outbox_handlers_and_dispatch_observer()
    {
        var provider = BuildProvider([]);

        using var scope = provider.CreateScope();
        var scopedProvider = scope.ServiceProvider;
        var handlers = scopedProvider.GetServices<IOutboxMessageHandler>().ToList();

        handlers.Select(x => x.MessageType).Should().BeEquivalentTo(
        [
            "RewriteJobCreated",
            StripeNotificationOutboxMessageTypes.PaymentFailed,
            StripeNotificationOutboxMessageTypes.PaymentRecovered,
            StripeNotificationOutboxMessageTypes.SubscriptionPaused,
            StripeNotificationOutboxMessageTypes.PaymentGraceReminder,
        ]);
        handlers.Select(x => x.MessageType).Should().OnlyHaveUniqueItems();
        scopedProvider.GetRequiredService<IOutboxDispatchObserver>().Should().NotBeNull();
    }

    [Fact]
    public void AddReplyInMyVoiceInfrastructure_does_not_register_retired_stripe_services()
    {
        var services = BuildServiceCollection([]);
        var registeredTypeNames = services
            .SelectMany(descriptor => new[]
            {
                descriptor.ServiceType.Name,
                descriptor.ImplementationType?.Name,
            })
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToList();

        registeredTypeNames.Should().NotContain("StripeEventService");
        registeredTypeNames.Should().NotContain("StripeReconciliationService");
    }

    [Fact]
    public void AddReplyInMyVoiceInfrastructure_fails_fast_in_non_development_environments_when_critical_config_is_missing()
    {
        var values = new Dictionary<string, string?>
        {
            ["OPENAI_BASE_URL"] = "https://api.deepseek.com",
        };

        var act = () => BuildProvider(
            values,
            environmentName: "Production",
            requireServiceBusConsumer: true);

        var exception = act.Should().Throw<InvalidOperationException>().Which;
        exception.Message.Should().Contain("ConnectionStrings:DefaultConnection");
        exception.Message.Should().Contain("DATABASE_URL");
        exception.Message.Should().Contain("ConnectionStrings:ServiceBus");
        exception.Message.Should().Contain("SERVICEBUS_CONNECTION_STRING");
        exception.Message.Should().Contain("AZURE_SERVICE_BUS_CONNECTION_STRING");
        exception.Message.Should().Contain("STRIPE_SECRET_KEY");
        exception.Message.Should().Contain("STRIPE_WEBHOOK_SECRET");
        exception.Message.Should().Contain("DEEPSEEK_API_KEY");
        exception.Message.Should().Contain("OPENAI_API_KEY");
        exception.Message.Should().Contain("SAPLING_API_KEY");
        exception.Message.Should().NotContain("https://api.deepseek.com");
    }

    [Fact]
    public void AddReplyInMyVoiceInfrastructure_requires_service_bus_connection_only_when_consumer_is_enabled()
    {
        var values = CompleteProductionConfiguration();

        using var provider = BuildProvider(values, environmentName: "Production");

        var act = () => BuildProvider(
            values,
            environmentName: "Production",
            requireServiceBusConsumer: true);

        var exception = act.Should().Throw<InvalidOperationException>().Which;
        exception.Message.Should().Contain("ConnectionStrings:ServiceBus");
        exception.Message.Should().Contain("SERVICEBUS_CONNECTION_STRING");
        exception.Message.Should().Contain("AZURE_SERVICE_BUS_CONNECTION_STRING");
        exception.Message.Should().NotContain("DATABASE_URL");
        exception.Message.Should().NotContain("STRIPE_SECRET_KEY");
        exception.Message.Should().NotContain("SAPLING_API_KEY");
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Testing")]
    public void AddReplyInMyVoiceInfrastructure_keeps_local_fallbacks_for_development_and_testing(string environmentName)
    {
        var provider = BuildProvider([], environmentName);

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
    public void AddReplyInMyVoiceInfrastructure_webhook_http_client_disables_redirects_and_uses_connect_guard()
    {
        var provider = BuildProvider([]);
        var handlerFactory = provider.GetRequiredService<IHttpMessageHandlerFactory>();

        using var handler = handlerFactory.CreateHandler(nameof(ReplyInMyVoice.Infrastructure.Services.IWebhookDeliverySender));
        var socketsHandler = FindHandler<SocketsHttpHandler>(handler);

        socketsHandler.Should().NotBeNull();
        socketsHandler!.AllowAutoRedirect.Should().BeFalse();
        socketsHandler.ConnectCallback.Should().NotBeNull();
        socketsHandler.ConnectTimeout.Should().BeLessThanOrEqualTo(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void AddReplyInMyVoiceInfrastructure_registers_singleton_circuit_breaker_registry_with_default_options()
    {
        var provider = BuildProvider([]);

        var firstRegistry = provider.GetRequiredService<ProviderCircuitBreakerRegistry>();
        var secondRegistry = provider.GetRequiredService<ProviderCircuitBreakerRegistry>();
        var options = provider.GetRequiredService<ProviderCircuitBreakerOptions>();

        firstRegistry.Should().BeSameAs(secondRegistry);
        options.SamplingDuration.Should().Be(TimeSpan.FromSeconds(30));
        options.BreakDuration.Should().Be(TimeSpan.FromSeconds(30));
        options.MinimumThroughput.Should().Be(8);
        options.FailureRatio.Should().Be(0.5);
    }

    [Fact]
    public void AddReplyInMyVoiceInfrastructure_binds_circuit_breaker_options_from_environment_and_clamps_invalid_values()
    {
        var validProvider = BuildProvider(new Dictionary<string, string?>
        {
            ["PROVIDER_CIRCUIT_SAMPLING_SEC"] = "10",
            ["PROVIDER_CIRCUIT_BREAK_SEC"] = "60",
            ["PROVIDER_CIRCUIT_MIN_SAMPLES"] = "4",
            ["PROVIDER_CIRCUIT_FAILURE_RATIO"] = "0.25",
        });
        var invalidProvider = BuildProvider(new Dictionary<string, string?>
        {
            ["PROVIDER_CIRCUIT_MIN_SAMPLES"] = "0",
            ["PROVIDER_CIRCUIT_FAILURE_RATIO"] = "2.0",
        });

        var validOptions = validProvider.GetRequiredService<ProviderCircuitBreakerOptions>();
        var invalidOptions = invalidProvider.GetRequiredService<ProviderCircuitBreakerOptions>();

        validOptions.SamplingDuration.Should().Be(TimeSpan.FromSeconds(10));
        validOptions.BreakDuration.Should().Be(TimeSpan.FromSeconds(60));
        validOptions.MinimumThroughput.Should().Be(4);
        validOptions.FailureRatio.Should().Be(0.25);
        invalidOptions.MinimumThroughput.Should().Be(8);
        invalidOptions.FailureRatio.Should().Be(0.5);
    }

    [Fact]
    public void AddReplyInMyVoiceInfrastructure_attaches_resilience_handler_to_provider_clients()
    {
        var provider = BuildProvider([]);
        var handlerFactory = provider.GetRequiredService<IHttpMessageHandlerFactory>();

        using var modelHandler = handlerFactory.CreateHandler(nameof(OpenAiCompatibleRewriteModelClient));
        using var signalHandler = handlerFactory.CreateHandler(nameof(SaplingWritingSignalClient));

        FindHandler<ProviderHttpResilienceHandler>(modelHandler).Should().NotBeNull();
        FindHandler<ProviderHttpResilienceHandler>(signalHandler).Should().NotBeNull();
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

    private static ServiceProvider BuildProvider(
        Dictionary<string, string?> values,
        string environmentName = "Testing",
        bool requireServiceBusConsumer = false)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        var services = BuildServiceCollection(
            values,
            environmentName,
            requireServiceBusConsumer);
        return services.BuildServiceProvider();
    }

    private static ServiceCollection BuildServiceCollection(
        Dictionary<string, string?> values,
        string environmentName = "Testing",
        bool requireServiceBusConsumer = false)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        var services = new ServiceCollection();
        services.AddReplyInMyVoiceInfrastructure(
            configuration,
            environmentName,
            requireServiceBusConsumer);
        return services;
    }

    private static Dictionary<string, string?> CompleteProductionConfiguration() => new()
    {
        ["DATABASE_URL"] = "Server=localhost;Database=ReplyInMyVoiceTest;User Id=test;Password=test;TrustServerCertificate=True",
        ["STRIPE_SECRET_KEY"] = "stripe-test-key",
        ["STRIPE_WEBHOOK_SECRET"] = "stripe-webhook-test-key",
        ["OPENAI_BASE_URL"] = "https://api.deepseek.com",
        ["DEEPSEEK_API_KEY"] = "deepseek-test-key",
        ["SAPLING_API_KEY"] = "sapling-test-key",
    };

    private static FactReconstructRewriteOptions ReadFactReconstructOptions(IRewriteProvider provider)
    {
        var optionsField = typeof(FactReconstructRewriteProvider).GetField(
            "_options",
            BindingFlags.Instance | BindingFlags.NonPublic);

        optionsField.Should().NotBeNull();
        return (FactReconstructRewriteOptions)optionsField!.GetValue(provider)!;
    }

    private static THandler? FindHandler<THandler>(HttpMessageHandler handler)
        where THandler : HttpMessageHandler
    {
        if (handler is THandler typed)
        {
            return typed;
        }

        if (handler is DelegatingHandler delegatingHandler && delegatingHandler.InnerHandler is not null)
        {
            return FindHandler<THandler>(delegatingHandler.InnerHandler);
        }

        var innerHandlerField = handler.GetType().GetField(
            "_innerHandler",
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (innerHandlerField?.GetValue(handler) is HttpMessageHandler innerHandler)
        {
            return FindHandler<THandler>(innerHandler);
        }

        return null;
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
