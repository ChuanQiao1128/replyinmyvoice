using Azure.Messaging.ServiceBus;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReplyInMyVoice.Infrastructure.Data;
using ReplyInMyVoice.Infrastructure.Providers;
using ReplyInMyVoice.Infrastructure.Queueing;
using ReplyInMyVoice.Infrastructure.Services;

namespace ReplyInMyVoice.Infrastructure;

public static class ServiceCollectionExtensions
{
    private const int DefaultTotalRewriteBudgetSeconds = 180;

    public static IServiceCollection AddReplyInMyVoiceInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        string? environmentName = null,
        bool requireServiceBusConsumer = false)
    {
        configuration.ValidateReplyInMyVoiceRuntimeConfiguration(
            environmentName,
            requireServiceBusConsumer);

        services.AddDbContext<AppDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? configuration["DATABASE_URL"];

            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                options.UseSqlServer(
                    connectionString,
                    sqlOptions => sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(10),
                        errorNumbersToAdd: null));
            }
            else
            {
                options.UseSqlite("Data Source=replyinmyvoice-dev.db");
            }
        });

        services.AddScoped<Func<AppDbContext>>(sp =>
        {
            var options = sp.GetRequiredService<DbContextOptions<AppDbContext>>();
            return () => new AppDbContext(options);
        });
        services.AddScoped<AccountService>();
        services.AddScoped<QuotaService>();
        services.AddScoped<AccountService>();
        services.AddScoped<RewriteRequestService>();
        services.AddScoped<RewriteJobProcessor>();
        services.AddScoped<OutboxDispatcherService>();
        services.AddScoped<ExpiredReservationCleanupService>();
        services.AddScoped<StripeEventService>();
        services.AddScoped<IStripeBillingService, StripeBillingService>();
        services.AddHttpClient();

        var serviceBusConnection = configuration.GetConnectionString("ServiceBus")
            ?? configuration["ServiceBus"]
            ?? configuration["SERVICEBUS_CONNECTION_STRING"]
            ?? configuration["AZURE_SERVICE_BUS_CONNECTION_STRING"];
        var queueName = configuration["ServiceBus:QueueName"]
            ?? configuration["SERVICEBUS_QUEUE_NAME"]
            ?? configuration["AZURE_SERVICE_BUS_QUEUE"]
            ?? "rewrite-jobs";

        if (!string.IsNullOrWhiteSpace(serviceBusConnection))
        {
            services.AddSingleton(_ => new ServiceBusClient(serviceBusConnection));
            services.AddSingleton(sp => sp.GetRequiredService<ServiceBusClient>().CreateSender(queueName));
            services.AddSingleton<IRewriteJobPublisher, AzureServiceBusRewriteJobPublisher>();
        }
        else
        {
            services.AddSingleton<InMemoryRewriteJobPublisher>();
            services.AddSingleton<IRewriteJobPublisher>(sp => sp.GetRequiredService<InMemoryRewriteJobPublisher>());
        }

        var openAiBaseUrl = configuration["OPENAI_BASE_URL"] ?? "https://api.openai.com/v1";
        var modelApiKey = ResolveOpenAiCompatibleApiKey(configuration, openAiBaseUrl);
        var saplingApiKey = configuration["SAPLING_API_KEY"];
        var model = configuration["OPENAI_MODEL_MID_WRITER"]
            ?? configuration["OPENAI_MODEL"]
            ?? "gpt-4o-mini";
        var openAiTimeoutSeconds = int.TryParse(configuration["OPENAI_TIMEOUT_SEC"], out var parsedOpenAiTimeout)
            ? parsedOpenAiTimeout
            : 60;
        var signalTimeoutSeconds = int.TryParse(configuration["WRITING_SIGNAL_TIMEOUT_SEC"], out var parsedSignalTimeout)
            ? parsedSignalTimeout
            : 10;

        // Adaptive refinement loop: refine until a send-ready candidate reaches the AI-signal
        // target, then return it (or the lowest-scoring one once the attempt budget is spent —
        // soft target, never fail-closed). Validated 2026-05-26: target 20 / max 10 loops drove
        // all 100 eval cases under 25 with zero fact loss, ~1.7 model calls/case. Both tunable
        // via app settings without a redeploy.
        var aiSignalTarget = int.TryParse(configuration["AI_SIGNAL_TARGET"], out var parsedTarget)
            ? parsedTarget
            : 20;
        var rewriteMaxAttempts = int.TryParse(configuration["REWRITE_MAX_ATTEMPTS"], out var parsedMaxAttempts)
            ? parsedMaxAttempts
            : 10;
        // Production wall-clock cap for the whole rewrite (all loops combined). An explicit
        // TOTAL_REWRITE_BUDGET_SEC=0 still leaves it unlimited for controlled non-production runs.
        var totalRewriteBudgetSeconds = DefaultTotalRewriteBudgetSeconds;
        if (configuration["TOTAL_REWRITE_BUDGET_SEC"] is { } configuredBudget &&
            int.TryParse(configuredBudget, out var parsedBudget) &&
            parsedBudget >= 0)
        {
            totalRewriteBudgetSeconds = parsedBudget;
        }

        if (string.IsNullOrWhiteSpace(modelApiKey) && string.IsNullOrWhiteSpace(saplingApiKey))
        {
            services.AddScoped<IRewriteProvider, DeterministicRewriteProvider>();
        }
        else
        {
            services.AddScoped<IRewriteModelClient>(sp =>
            {
                if (string.IsNullOrWhiteSpace(modelApiKey))
                {
                    return new UnavailableRewriteModelClient("model_not_configured");
                }

                var clientFactory = sp.GetRequiredService<IHttpClientFactory>();
                return new OpenAiCompatibleRewriteModelClient(
                    clientFactory.CreateClient(nameof(OpenAiCompatibleRewriteModelClient)),
                    modelApiKey,
                    model,
                    openAiBaseUrl,
                    TimeSpan.FromSeconds(openAiTimeoutSeconds));
            });
            services.AddScoped<IWritingSignalClient>(sp =>
            {
                if (string.IsNullOrWhiteSpace(saplingApiKey))
                {
                    return new UnavailableWritingSignalClient("sapling_not_configured");
                }

                var clientFactory = sp.GetRequiredService<IHttpClientFactory>();
                return new SaplingWritingSignalClient(
                    clientFactory.CreateClient(nameof(SaplingWritingSignalClient)),
                    saplingApiKey,
                    TimeSpan.FromSeconds(signalTimeoutSeconds));
            });
            services.AddScoped<IRewriteProvider>(sp => new FactReconstructRewriteProvider(
                sp.GetRequiredService<IRewriteModelClient>(),
                sp.GetRequiredService<IWritingSignalClient>(),
                new FactReconstructRewriteOptions(
                    RequestedMaxAttempts: rewriteMaxAttempts,
                    TargetAiLikePercent: aiSignalTarget,
                    TotalTimeBudget: TimeSpan.FromSeconds(totalRewriteBudgetSeconds))));
        }

        return services;
    }

    public static void ValidateReplyInMyVoiceRuntimeConfiguration(
        this IConfiguration configuration,
        string? environmentName,
        bool requireServiceBusConsumer = false)
    {
        var runtimeEnvironmentName = ResolveRuntimeEnvironmentName(configuration, environmentName);
        if (string.IsNullOrWhiteSpace(runtimeEnvironmentName) ||
            IsDevelopmentOrTesting(runtimeEnvironmentName))
        {
            return;
        }

        var missing = new List<string>();

        if (string.IsNullOrWhiteSpace(configuration.GetConnectionString("DefaultConnection")) &&
            string.IsNullOrWhiteSpace(configuration["DATABASE_URL"]))
        {
            missing.Add("ConnectionStrings:DefaultConnection or DATABASE_URL");
        }

        if (requireServiceBusConsumer &&
            string.IsNullOrWhiteSpace(configuration.GetConnectionString("ServiceBus")) &&
            string.IsNullOrWhiteSpace(configuration["ServiceBus"]) &&
            string.IsNullOrWhiteSpace(configuration["SERVICEBUS_CONNECTION_STRING"]) &&
            string.IsNullOrWhiteSpace(configuration["AZURE_SERVICE_BUS_CONNECTION_STRING"]))
        {
            missing.Add("ConnectionStrings:ServiceBus or ServiceBus or SERVICEBUS_CONNECTION_STRING or AZURE_SERVICE_BUS_CONNECTION_STRING");
        }

        if (string.IsNullOrWhiteSpace(configuration["STRIPE_SECRET_KEY"]))
        {
            missing.Add("STRIPE_SECRET_KEY");
        }

        if (string.IsNullOrWhiteSpace(configuration["STRIPE_WEBHOOK_SECRET"]))
        {
            missing.Add("STRIPE_WEBHOOK_SECRET");
        }

        var openAiBaseUrl = configuration["OPENAI_BASE_URL"] ?? "https://api.openai.com/v1";
        if (string.IsNullOrWhiteSpace(ResolveOpenAiCompatibleApiKey(configuration, openAiBaseUrl)))
        {
            missing.Add(IsDeepSeekBaseUrl(openAiBaseUrl)
                ? "DEEPSEEK_API_KEY or OPENAI_API_KEY"
                : "OPENAI_API_KEY or DEEPSEEK_API_KEY");
        }

        if (string.IsNullOrWhiteSpace(configuration["SAPLING_API_KEY"]))
        {
            missing.Add("SAPLING_API_KEY");
        }

        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                $"ReplyInMyVoice startup configuration is missing required setting(s) for {runtimeEnvironmentName}: {string.Join(", ", missing)}.");
        }
    }

    private static string? ResolveRuntimeEnvironmentName(IConfiguration configuration, string? environmentName) =>
        !string.IsNullOrWhiteSpace(environmentName)
            ? environmentName
            : configuration["DOTNET_ENVIRONMENT"]
                ?? configuration["ASPNETCORE_ENVIRONMENT"]
                ?? configuration["AZURE_FUNCTIONS_ENVIRONMENT"];

    private static bool IsDevelopmentOrTesting(string environmentName) =>
        string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(environmentName, "Testing", StringComparison.OrdinalIgnoreCase);

    private static string? ResolveOpenAiCompatibleApiKey(IConfiguration configuration, string baseUrl)
    {
        if (IsDeepSeekBaseUrl(baseUrl))
        {
            return configuration["DEEPSEEK_API_KEY"] ?? configuration["OPENAI_API_KEY"];
        }

        return configuration["OPENAI_API_KEY"] ?? configuration["DEEPSEEK_API_KEY"];
    }

    private static bool IsDeepSeekBaseUrl(string value)
    {
        try
        {
            var host = new Uri(value).Host.ToLowerInvariant();
            return host == "api.deepseek.com" || host.EndsWith(".deepseek.com", StringComparison.Ordinal);
        }
        catch (UriFormatException)
        {
            return false;
        }
    }
}
