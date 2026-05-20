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
        services.AddScoped<QuotaService>();
        services.AddScoped<RewriteRequestService>();
        services.AddScoped<RewriteJobProcessor>();
        services.AddScoped<OutboxDispatcherService>();
        services.AddScoped<ExpiredReservationCleanupService>();
        services.AddScoped<StripeEventService>();
        services.AddScoped<IStripeBillingService, StripeBillingService>();
        services.AddHttpClient();

        var serviceBusConnection = configuration.GetConnectionString("ServiceBus")
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

        services.AddScoped<IRewriteProvider>(sp =>
        {
            var apiKey = configuration["OPENAI_API_KEY"];
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return new DeterministicRewriteProvider();
            }

            var clientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var model = configuration["OPENAI_MODEL"] ?? "gpt-4o-mini";
            var timeoutSeconds = int.TryParse(configuration["OPENAI_TIMEOUT_SEC"], out var parsed)
                ? parsed
                : 25;
            return new OpenAiRewriteProvider(
                clientFactory.CreateClient(nameof(OpenAiRewriteProvider)),
                apiKey,
                model,
                TimeSpan.FromSeconds(timeoutSeconds));
        });

        return services;
    }
}
