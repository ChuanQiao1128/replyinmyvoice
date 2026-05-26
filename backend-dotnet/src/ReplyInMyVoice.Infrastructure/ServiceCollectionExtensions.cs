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
    public static IServiceCollection AddReplyInMyVoiceInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? configuration["DATABASE_URL"];

            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                options.UseSqlServer(connectionString);
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
                    TargetAiLikePercent: aiSignalTarget)));
        }

        return services;
    }

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
