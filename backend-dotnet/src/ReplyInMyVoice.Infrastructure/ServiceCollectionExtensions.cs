using System.Globalization;
using Azure.Messaging.ServiceBus;
using Microsoft.ApplicationInsights;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.Common;
using ReplyInMyVoice.Application.UseCases.Account;
using ReplyInMyVoice.Application.UseCases.Admin;
using ReplyInMyVoice.Application.UseCases.ApiKey;
using ReplyInMyVoice.Application.UseCases.Billing;
using ReplyInMyVoice.Application.UseCases.BillingSupport;
using ReplyInMyVoice.Application.UseCases.CreditExpiry;
using ReplyInMyVoice.Application.UseCases.Promo;
using ReplyInMyVoice.Application.UseCases.PromoAdmin;
using ReplyInMyVoice.Application.UseCases.Quota;
using ReplyInMyVoice.Application.UseCases.Rewrite;
using ReplyInMyVoice.Application.UseCases.RewriteJob;
using ReplyInMyVoice.Application.UseCases.StripeEvent;
using ReplyInMyVoice.Application.UseCases.StripeReconciliation;
using ReplyInMyVoice.Application.UseCases.WebhookOutbox;
using ReplyInMyVoice.Infrastructure.Data;
using ReplyInMyVoice.Infrastructure.Notifications;
using ReplyInMyVoice.Infrastructure.Providers;
using ReplyInMyVoice.Infrastructure.Queueing;
using ReplyInMyVoice.Infrastructure.Repositories;
using ReplyInMyVoice.Infrastructure.Resilience;
using ReplyInMyVoice.Infrastructure.Services;
using AppStripeBillingClient = ReplyInMyVoice.Application.Abstractions.IStripeBillingClient;
using AppStripePaymentReconciliationClient = ReplyInMyVoice.Application.Abstractions.IStripePaymentReconciliationClient;
using AppStripeRefundClient = ReplyInMyVoice.Application.Abstractions.IStripeRefundClient;
using AppStripeReconciliationAlerter = ReplyInMyVoice.Application.Abstractions.IStripeReconciliationAlerter;
using LegacyStripePaymentReconciliationClient = ReplyInMyVoice.Infrastructure.Services.IStripePaymentReconciliationClient;
using LegacyStripeRefundClient = ReplyInMyVoice.Infrastructure.Services.IStripeRefundClient;
using LegacyStripeReconciliationAlerter = ReplyInMyVoice.Infrastructure.Services.IStripeReconciliationAlerter;

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

        services.AddLogging();

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
        services.AddScoped<IAppUserRepository, AppUserRepository>();
        services.AddScoped<IUsagePeriodRepository, UsagePeriodRepository>();
        services.AddScoped<IRewriteAttemptRepository, RewriteAttemptRepository>();
        services.AddScoped<IUsageReservationRepository, UsageReservationRepository>();
        services.AddScoped<IRewriteCreditRepository, RewriteCreditRepository>();
        services.AddScoped<IOutboxMessageRepository, OutboxMessageRepository>();
        services.AddScoped<IWebhookDeliveryRepository, WebhookDeliveryRepository>();
        services.AddScoped<IPromoCodeRepository, PromoCodeRepository>();
        services.AddScoped<IPromoCodeRedemptionRepository, PromoCodeRedemptionRepository>();
        services.AddScoped<IPromoAdminRepository, PromoAdminRepository>();
        services.AddScoped<IStripeEventRepository, StripeEventRepository>();
        services.AddScoped<IStripeInvoiceRepository, StripeInvoiceRepository>();
        services.AddScoped<IBillingSupportRepository, BillingSupportRepository>();
        services.AddScoped<IBillingSupportRequestRepository, BillingSupportRequestRepository>();
        services.AddScoped<IPaymentGrantRepository, PaymentGrantRepository>();
        services.AddScoped<IStripeReconciliationRunRepository, StripeReconciliationRunRepository>();
        services.AddScoped<IApiKeyRepository, ApiKeyRepository>();
        services.AddScoped<IApiKeyUsageRepository, ApiKeyUsageRepository>();
        services.AddScoped<IAdminUserRepository, AdminUserRepository>();
        services.AddScoped<IAdminStatsRepository, AdminStatsRepository>();
        services.AddScoped<IAccountUsagePlanProvider>(_ => new AccountUsagePlanProvider(configuration));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IRewriteEngineClient, RewriteProviderEngineClient>();
        services.AddScoped<IRewriteCostLogger, RewriteCostLogger>();
        var outboxFastPathEnabled = !bool.TryParse(
            configuration["OUTBOX_FAST_PATH_ENABLED"],
            out var parsedOutboxFastPathEnabled) || parsedOutboxFastPathEnabled;
        var outboxFastPathTimeoutSeconds = int.TryParse(
            configuration["OUTBOX_FAST_PATH_TIMEOUT_SEC"],
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var parsedOutboxFastPathTimeoutSeconds)
            ? Math.Clamp(parsedOutboxFastPathTimeoutSeconds, 1, 30)
            : 5;
        var outboxFastPathOptions = new OutboxFastPathOptions(
            outboxFastPathEnabled,
            TimeSpan.FromSeconds(outboxFastPathTimeoutSeconds));
        services.AddSingleton(outboxFastPathOptions);
        services.AddScoped<ICreditExpiryNotifier, CreditExpiryNotifier>();
        services.AddScoped<ITaxTurnoverNotifier, TaxTurnoverNotifier>();
        services.AddScoped<ITaxTurnoverSettingsProvider, TaxTurnoverSettingsProvider>();
        services.AddScoped<GetOrCreateUserHandler>();
        services.AddScoped<FindUserHandler>();
        services.AddScoped<GetAccountSummaryHandler>();
        services.AddScoped<GetPurchaseHistoryHandler>();
        services.AddScoped<HasPaidApiEntitlementHandler>();
        services.AddScoped<GetBillingHistoryHandler>();
        services.AddScoped<CreateBillingSupportRequestHandler>();
        services.AddScoped<GetBillingSupportRequestsHandler>();
        services.AddScoped<DeleteAccountHandler>();
        services.AddScoped<GetAdminUsersHandler>();
        services.AddScoped<GetAdminUserDetailHandler>();
        services.AddScoped<GetAdminStatsHandler>();
        services.AddScoped<GrantCreditsHandler>();
        services.AddScoped<DeleteAdminUserHandler>();
        services.AddScoped<GetBillingSupportQueueHandler>();
        services.AddScoped<ResolveBillingSupportRequestHandler>();
        services.AddScoped<ExportAccountingRevenueHandler>();
        services.AddScoped<SetUserSuspensionHandler>();
        services.AddScoped<IssueRefundHandler>();
        services.AddScoped<CreateCheckoutSessionHandler>();
        services.AddScoped<CreatePortalSessionHandler>();
        services.AddScoped<CancelSubscriptionHandler>();
        services.AddScoped<RefundPaymentHandler>();
        services.AddScoped<ListPaidPaymentsHandler>();
        services.AddScoped<GetTaxTurnoverReportHandler>();
        services.AddScoped<ReserveQuotaHandler>();
        services.AddScoped<FinalizeQuotaSuccessHandler>();
        services.AddScoped<MarkQuotaProcessingHandler>();
        services.AddScoped<ReleaseQuotaHandler>();
        services.AddScoped<ReleaseExpiredReservationsHandler>();
        services.AddScoped<RedeemPromoHandler>();
        services.AddScoped<GetPromoStatusHandler>();
        services.AddScoped<ListPromoCodesHandler>();
        services.AddScoped<GetPromoCodeDetailHandler>();
        services.AddScoped<CreatePromoCodeHandler>();
        services.AddScoped<UpdatePromoCodeHandler>();
        services.AddScoped<SetPromoCodeActiveHandler>();
        services.AddScoped<ArchivePromoCodeHandler>();
        services.AddScoped<RestorePromoCodeHandler>();
        services.AddScoped<CreateRewriteAttemptHandler>();
        services.AddScoped<GetRewriteAttemptHandler>();
        services.AddScoped<DispatchDueWebhooksHandler>();
        services.AddScoped<DispatchDueOutboxHandler>();
        services.AddScoped<IOutboxFastPathDispatcher>(sp => new OutboxFastPathDispatcher(
            sp.GetRequiredService<DispatchDueOutboxHandler>(),
            sp.GetRequiredService<OutboxFastPathOptions>(),
            sp.GetRequiredService<ILogger<OutboxFastPathDispatcher>>()));
        services.AddScoped<ReconcileStripeHandler>();
        services.AddScoped<GenerateApiKeyHandler>();
        services.AddScoped<ListApiKeysHandler>();
        services.AddScoped<RotateApiKeyHandler>();
        services.AddScoped<RevokeApiKeyHandler>();
        services.AddScoped<SetApiKeyWebhookHandler>();
        services.AddScoped<ClearApiKeyWebhookHandler>();
        services.AddScoped<GetApiUsageSummaryHandler>();
        services.AddScoped<GetApiUsageSeriesHandler>();
        services.AddScoped<GetApiUsageRecentHandler>();
        services.AddScoped<SendCreditExpiryRemindersHandler>();
        services.AddScoped<ProcessRewriteJobHandler>();
        services.AddScoped<TryMarkStripeEventProcessedHandler>();
        services.AddScoped<IngestStripeWebhookHandler>();
        services.AddScoped<ProcessPendingStripeEventsHandler>();
        services.AddScoped<StripeEventPayloadSynchronizer>();
        services.AddScoped<ProcessExpiredPaymentGraceHandler>();
        services.AddScoped<ProcessPaymentGraceRemindersHandler>();
        services.AddScoped<IApiKeyRateLimiter, ApiKeyRateLimiter>();
        services.AddScoped<WebhookDeliveryService>();
        services.AddScoped<IWebhookDeliveryEnqueuer>(sp => sp.GetRequiredService<WebhookDeliveryService>());
        services.AddTransient<IOutboxMessageHandler, RewriteJobCreatedOutboxMessageHandler>();
        services.AddTransient<IOutboxMessageHandler, PaymentFailedNotificationOutboxMessageHandler>();
        services.AddTransient<IOutboxMessageHandler, PaymentRecoveredNotificationOutboxMessageHandler>();
        services.AddTransient<IOutboxMessageHandler, SubscriptionPausedNotificationOutboxMessageHandler>();
        services.AddTransient<IOutboxMessageHandler, PaymentGraceReminderNotificationOutboxMessageHandler>();
        services.AddTransient<IOutboxMessageHandler, StripePaymentActionRequiredOutboxMessageHandler>();
        services.AddTransient<IOutboxMessageHandler, StripeCardExpiringOutboxMessageHandler>();
        services.AddTransient<IOutboxMessageHandler, StripeReconciliationAlertOutboxMessageHandler>();
        services.AddScoped<IOutboxDispatchObserver>(sp => new OutboxDispatchTelemetryObserver(
            sp.GetRequiredService<ILogger<OutboxDispatchTelemetryObserver>>(),
            sp.GetService<TelemetryClient>()));
        services.AddScoped<ExpiredReservationCleanupService>();
        services.AddScoped<RetentionService>();
        services.AddSingleton<ReplyInMyVoice.Infrastructure.Services.IStripeBillingClient, StripeBillingClient>();
        services.AddSingleton(ReadStripeEventProcessingOptions(configuration));
        services.AddSingleton(ReadProviderCircuitBreakerOptions(configuration));
        services.AddSingleton(ReadStripeReconciliationOptions(configuration));
        services.TryAddSingleton<IProviderResilienceEvents, NoOpProviderResilienceEvents>();
        services.AddSingleton(sp => new ProviderCircuitBreakerRegistry(
            sp.GetRequiredService<ProviderCircuitBreakerOptions>(),
            sp.GetRequiredService<ILoggerFactory>(),
            sp.GetRequiredService<IProviderResilienceEvents>()));
        services.AddScoped<ApplicationStripeBillingClient>(sp =>
            new ApplicationStripeBillingClient(
                configuration,
                sp.GetService<ReplyInMyVoice.Infrastructure.Services.IStripeBillingClient>()));
        services.AddScoped<AppStripeBillingClient>(sp => sp.GetRequiredService<ApplicationStripeBillingClient>());
        services.AddScoped<AppStripeRefundClient>(sp => sp.GetRequiredService<ApplicationStripeBillingClient>());
        services.AddScoped(sp => new StripeBillingService(
            sp.GetRequiredService<Func<AppDbContext>>(),
            configuration,
            sp.GetService<ReplyInMyVoice.Infrastructure.Services.IStripeBillingClient>()));
        services.AddScoped<IStripeBillingService>(sp => sp.GetRequiredService<StripeBillingService>());
        services.AddScoped<LegacyStripeRefundClient>(sp => sp.GetRequiredService<StripeBillingService>());
        services.AddScoped<IStripeEventNotifier, StripeEventNotifier>();
        services.AddScoped<IStripeSubscriptionCancellationService, StripeSubscriptionCancellationService>();
        services.AddScoped<LegacyStripePaymentReconciliationClient>(sp => sp.GetRequiredService<StripeBillingService>());
        services.AddScoped<AppStripePaymentReconciliationClient>(sp => new StripePaymentReconciliationClient(
            sp.GetRequiredService<AppStripeBillingClient>(),
            configuration));
        services.AddScoped<LegacyStripeReconciliationAlerter>(sp => new StripeReconciliationNotificationAlerter(
            configuration,
            sp.GetRequiredService<INotificationService>(),
            sp.GetRequiredService<ILogger<StripeReconciliationNotificationAlerter>>()));
        services.AddScoped<AppStripeReconciliationAlerter, StripeReconciliationAlerterAdapter>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddSingleton<ICheckoutVelocityLimiter, CheckoutVelocityLimiter>();
        services.AddHttpClient();
        services.AddTransient<ReplyInMyVoice.Application.Abstractions.IWebhookDeliverySender, WebhookDeliverySenderAdapter>();
        services.AddHttpClient<ReplyInMyVoice.Infrastructure.Services.IWebhookDeliverySender, HttpWebhookDeliverySender>(client =>
            {
                client.Timeout = WebhookHttpClientFactory.OverallTimeout;
            })
            .ConfigurePrimaryHttpMessageHandler(WebhookHttpClientFactory.CreateHandler);
        services.AddResilientProviderHttpClient(nameof(OpenAiCompatibleRewriteModelClient));
        services.AddResilientProviderHttpClient(nameof(SaplingWritingSignalClient));
        services.AddHttpClient(nameof(ResendNotificationEmailProvider));
        services.AddSingleton<INotificationEmailProvider>(sp =>
            CreateNotificationEmailProvider(configuration, sp));

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

    private static IServiceCollection AddResilientProviderHttpClient(
        this IServiceCollection services,
        string name)
    {
        services
            .AddHttpClient(name)
            .AddHttpMessageHandler(sp => new ProviderHttpResilienceHandler(
                sp.GetRequiredService<ProviderCircuitBreakerRegistry>().GetOrAdd(name)));

        return services;
    }

    private static ProviderCircuitBreakerOptions ReadProviderCircuitBreakerOptions(IConfiguration configuration) =>
        new(
            TimeSpan.FromSeconds(ReadPositiveInt(configuration, "PROVIDER_CIRCUIT_SAMPLING_SEC", 30)),
            TimeSpan.FromSeconds(ReadPositiveInt(configuration, "PROVIDER_CIRCUIT_BREAK_SEC", 30)),
            ReadPositiveInt(configuration, "PROVIDER_CIRCUIT_MIN_SAMPLES", 8),
            ReadFailureRatio(configuration, "PROVIDER_CIRCUIT_FAILURE_RATIO", 0.5));

    private static StripeEventProcessingOptions ReadStripeEventProcessingOptions(IConfiguration configuration) =>
        new(
            ReadBoundedInt(configuration, "STRIPE_EVENT_MAX_ATTEMPTS", defaultValue: 8, minimum: 1, maximum: 50),
            ReadBoundedInt(configuration, "STRIPE_WEBHOOK_INLINE_BUDGET_SEC", defaultValue: 8, minimum: 0, maximum: 20));
    private static StripeReconciliationOptions ReadStripeReconciliationOptions(IConfiguration configuration) =>
        new(
            ReadClampedInt(configuration, "RECONCILIATION_AUTO_GRANT_MAX", 10, 0, 100),
            ReadClampedInt(configuration, "RECONCILIATION_MIN_PAYMENT_AGE_MINUTES", 60, 0, 1440),
            ReadClampedInt(configuration, "RECONCILIATION_WINDOW_DAYS", 3, 1, 30));

    private static int ReadClampedInt(
        IConfiguration configuration,
        string name,
        int defaultValue,
        int min,
        int max) =>
        int.TryParse(
            configuration[name],
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var parsed)
            ? Math.Clamp(parsed, min, max)
            : defaultValue;

    private static int ReadPositiveInt(IConfiguration configuration, string name, int defaultValue) =>
        int.TryParse(
            configuration[name],
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var parsed) && parsed >= 1
            ? parsed
            : defaultValue;

    private static int ReadBoundedInt(
        IConfiguration configuration,
        string name,
        int defaultValue,
        int minimum,
        int maximum)
    {
        if (!int.TryParse(
                configuration[name],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var parsed))
        {
            return defaultValue;
        }

        return Math.Clamp(parsed, minimum, maximum);
    }

    private static double ReadFailureRatio(IConfiguration configuration, string name, double defaultValue) =>
        double.TryParse(
            configuration[name],
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var parsed) && parsed > 0 && parsed <= 1
            ? parsed
            : defaultValue;

    private static INotificationEmailProvider CreateNotificationEmailProvider(
        IConfiguration configuration,
        IServiceProvider serviceProvider)
    {
        var configuredProvider = configuration["NOTIFICATIONS_PROVIDER"];
        var normalizedProvider = (configuredProvider ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedProvider) ||
            normalizedProvider is "disabled" or "none" or "noop")
        {
            return new NoOpNotificationEmailProvider(
                "provider_disabled",
                serviceProvider.GetRequiredService<ILogger<NoOpNotificationEmailProvider>>());
        }

        if (normalizedProvider != "resend")
        {
            return new NoOpNotificationEmailProvider(
                "unsupported_provider",
                serviceProvider.GetRequiredService<ILogger<NoOpNotificationEmailProvider>>());
        }

        var apiKey = configuration["RESEND_API_KEY"];
        var fromEmail = configuration["NOTIFICATIONS_FROM_EMAIL"];
        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(fromEmail))
        {
            return new NoOpNotificationEmailProvider(
                "missing_resend_config",
                serviceProvider.GetRequiredService<ILogger<NoOpNotificationEmailProvider>>());
        }

        var clientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
        return new ResendNotificationEmailProvider(
            clientFactory.CreateClient(nameof(ResendNotificationEmailProvider)),
            apiKey,
            fromEmail,
            configuration["NOTIFICATIONS_REPLY_TO_EMAIL"],
            serviceProvider.GetRequiredService<ILogger<ResendNotificationEmailProvider>>());
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
