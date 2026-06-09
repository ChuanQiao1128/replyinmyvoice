using System.Net;
using Azure.Messaging.ServiceBus;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.UseCases.Account;
using ReplyInMyVoice.Application.UseCases.ApiKey;
using ReplyInMyVoice.Application.UseCases.Billing;
using ReplyInMyVoice.Application.UseCases.Promo;
using ReplyInMyVoice.Application.UseCases.PromoAdmin;
using ReplyInMyVoice.Application.UseCases.Quota;
using ReplyInMyVoice.Application.UseCases.Rewrite;
using ReplyInMyVoice.Application.UseCases.RewriteJob;
using ReplyInMyVoice.Application.UseCases.StripeEvent;
using ReplyInMyVoice.Application.UseCases.StripeReconciliation;
using ReplyInMyVoice.Infrastructure.Data;
using ReplyInMyVoice.Infrastructure.Notifications;
using ReplyInMyVoice.Infrastructure.Providers;
using ReplyInMyVoice.Infrastructure.Queueing;
using ReplyInMyVoice.Infrastructure.Repositories;
using ReplyInMyVoice.Infrastructure.Services;
using AppStripePaymentReconciliationClient = ReplyInMyVoice.Application.Abstractions.IStripePaymentReconciliationClient;
using AppStripeReconciliationAlerter = ReplyInMyVoice.Application.Abstractions.IStripeReconciliationAlerter;
using LegacyStripePaymentReconciliationClient = ReplyInMyVoice.Infrastructure.Services.IStripePaymentReconciliationClient;
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
        services.AddScoped<IPromoCodeRepository, PromoCodeRepository>();
        services.AddScoped<IPromoCodeRedemptionRepository, PromoCodeRedemptionRepository>();
        services.AddScoped<IPromoAdminRepository, PromoAdminRepository>();
        services.AddScoped<IStripeEventRepository, StripeEventRepository>();
        services.AddScoped<IStripeInvoiceRepository, StripeInvoiceRepository>();
        services.AddScoped<IBillingSupportRequestRepository, BillingSupportRequestRepository>();
        services.AddScoped<IPaymentGrantRepository, PaymentGrantRepository>();
        services.AddScoped<IApiKeyRepository, ApiKeyRepository>();
        services.AddScoped<IApiKeyUsageRepository, ApiKeyUsageRepository>();
        services.AddScoped<IAccountUsagePlanProvider>(_ => new AccountUsagePlanProvider(configuration));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IRewriteEngineClient, RewriteProviderEngineClient>();
        services.AddScoped<IRewriteCostLogger, RewriteCostLogger>();
        services.AddScoped<ITaxTurnoverNotifier, TaxTurnoverNotifier>();
        services.AddScoped<ITaxTurnoverSettingsProvider, TaxTurnoverSettingsProvider>();
        services.AddScoped<GetOrCreateUserHandler>();
        services.AddScoped<FindUserHandler>();
        services.AddScoped<GetAccountSummaryHandler>();
        services.AddScoped<GetPurchaseHistoryHandler>();
        services.AddScoped<HasPaidApiEntitlementHandler>();
        services.AddScoped<GetBillingHistoryHandler>();
        services.AddScoped<DeleteAccountHandler>();
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
        services.AddScoped<ProcessRewriteJobHandler>();
        services.AddScoped<TryMarkStripeEventProcessedHandler>();
        services.AddScoped<ProcessStripeWebhookHandler>();
        services.AddScoped<ProcessExpiredPaymentGraceHandler>();
        services.AddScoped<ProcessPaymentGraceRemindersHandler>();
        services.AddScoped<AccountService>();
        services.AddScoped<ApiKeyService>();
        services.AddScoped<IApiKeyRateLimiter, ApiKeyRateLimiter>();
        services.AddScoped<ApiKeyUsageAnomalyService>();
        services.AddScoped<ApiKeyUsageQueryService>();
        services.AddScoped<WebhookDeliveryService>();
        services.AddScoped<IWebhookDeliveryEnqueuer>(sp => sp.GetRequiredService<WebhookDeliveryService>());
        services.AddScoped<WebhookDispatcherService>();
        services.AddScoped<QuotaService>();
        services.AddScoped<PromoService>();
        services.AddScoped<RewriteRequestService>();
        services.AddScoped<RewriteJobProcessor>();
        services.AddScoped<OutboxDispatcherService>();
        services.AddScoped<ExpiredReservationCleanupService>();
        services.AddScoped<RetentionService>();
        services.AddScoped<CreditExpiryReminderService>();
        services.AddScoped<StripeReconciliationService>();
        services.AddScoped<StripeEventService>();
        services.AddScoped<BillingSupportService>();
        services.AddSingleton<ReplyInMyVoice.Infrastructure.Services.IStripeBillingClient, StripeBillingClient>();
        services.AddSingleton<ReplyInMyVoice.Application.Abstractions.IStripeBillingClient>(sp =>
            new ApplicationStripeBillingClient(
                configuration,
                sp.GetService<ReplyInMyVoice.Infrastructure.Services.IStripeBillingClient>()));
        services.AddScoped<TaxTurnoverService>();
        services.AddScoped(sp => new StripeBillingService(
            sp.GetRequiredService<Func<AppDbContext>>(),
            configuration,
            sp.GetService<ReplyInMyVoice.Infrastructure.Services.IStripeBillingClient>()));
        services.AddScoped<IStripeBillingService>(sp => sp.GetRequiredService<StripeBillingService>());
        services.AddScoped<IStripeEventNotifier, StripeEventNotifier>();
        services.AddScoped<IStripeSubscriptionCancellationService, StripeSubscriptionCancellationService>();
        services.AddScoped<LegacyStripePaymentReconciliationClient>(sp => sp.GetRequiredService<StripeBillingService>());
        services.AddScoped<AppStripePaymentReconciliationClient, StripePaymentReconciliationClient>();
        services.AddScoped<LegacyStripeReconciliationAlerter>(sp => new StripeReconciliationNotificationAlerter(
            configuration,
            sp.GetRequiredService<INotificationService>(),
            sp.GetRequiredService<ILogger<StripeReconciliationNotificationAlerter>>()));
        services.AddScoped<AppStripeReconciliationAlerter, StripeReconciliationAlerterAdapter>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddSingleton<ICheckoutVelocityLimiter, CheckoutVelocityLimiter>();
        services.AddHttpClient();
        services.AddHttpClient<IWebhookDeliverySender, HttpWebhookDeliverySender>(client =>
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
            .AddResilienceHandler();

        return services;
    }

    private static IHttpClientBuilder AddResilienceHandler(this IHttpClientBuilder builder) =>
        builder.AddHttpMessageHandler(() => new ProviderHttpResilienceHandler());

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

    private sealed class ProviderHttpResilienceHandler : DelegatingHandler
    {
        private static readonly TimeSpan BaseRetryDelay = TimeSpan.FromMilliseconds(250);
        private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan CircuitSamplingDuration = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan CircuitBreakDuration = TimeSpan.FromSeconds(30);
        private const int MaxRetryAttempts = 3;
        private const int CircuitMinimumThroughput = 8;
        private const double CircuitFailureRatio = 0.5;

        private readonly object _circuitLock = new();
        private readonly Queue<CircuitSample> _circuitSamples = new();
        private DateTimeOffset? _circuitOpenUntil;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var snapshot = await BufferedHttpRequestSnapshot.CreateAsync(request, cancellationToken);

            for (var attempt = 0; attempt <= MaxRetryAttempts; attempt++)
            {
                ThrowIfCircuitOpen();

                try
                {
                    using var attemptRequest = snapshot.CreateRequest();
                    var response = await base.SendAsync(attemptRequest, cancellationToken);
                    if (!IsTransientStatusCode(response.StatusCode))
                    {
                        RecordCircuitResult(success: true);
                        return response;
                    }

                    if (attempt == MaxRetryAttempts)
                    {
                        RecordCircuitResult(success: false);
                        return response;
                    }

                    var delay = RetryDelay(attempt, response);
                    response.Dispose();
                    await Task.Delay(delay, cancellationToken);
                }
                catch (Exception exception) when (IsTransientException(exception, cancellationToken))
                {
                    if (attempt == MaxRetryAttempts)
                    {
                        RecordCircuitResult(success: false);
                        throw;
                    }

                    await Task.Delay(RetryDelay(attempt, response: null), cancellationToken);
                }
            }

            throw new InvalidOperationException("Provider HTTP retry loop exited unexpectedly.");
        }

        private void ThrowIfCircuitOpen()
        {
            var now = DateTimeOffset.UtcNow;
            lock (_circuitLock)
            {
                if (_circuitOpenUntil is null)
                {
                    return;
                }

                if (now < _circuitOpenUntil.Value)
                {
                    throw new HttpRequestException("Provider HTTP circuit is open.");
                }

                _circuitOpenUntil = null;
                _circuitSamples.Clear();
            }
        }

        private void RecordCircuitResult(bool success)
        {
            var now = DateTimeOffset.UtcNow;
            lock (_circuitLock)
            {
                while (_circuitSamples.Count > 0 &&
                       now - _circuitSamples.Peek().ObservedAt > CircuitSamplingDuration)
                {
                    _circuitSamples.Dequeue();
                }

                _circuitSamples.Enqueue(new CircuitSample(now, success));
                if (_circuitSamples.Count < CircuitMinimumThroughput)
                {
                    return;
                }

                var failures = _circuitSamples.Count(sample => !sample.Success);
                if ((double)failures / _circuitSamples.Count >= CircuitFailureRatio)
                {
                    _circuitOpenUntil = now + CircuitBreakDuration;
                }
            }
        }

        private static TimeSpan RetryDelay(int attempt, HttpResponseMessage? response)
        {
            if (response?.Headers.RetryAfter?.Delta is { } retryAfterDelta &&
                retryAfterDelta > TimeSpan.Zero)
            {
                return retryAfterDelta + Jitter();
            }

            if (response?.Headers.RetryAfter?.Date is { } retryAfterDate)
            {
                var retryAfter = retryAfterDate - DateTimeOffset.UtcNow;
                if (retryAfter > TimeSpan.Zero)
                {
                    return retryAfter + Jitter();
                }
            }

            var backoffMs = BaseRetryDelay.TotalMilliseconds * Math.Pow(2, attempt);
            var backoff = TimeSpan.FromMilliseconds(Math.Min(backoffMs, MaxRetryDelay.TotalMilliseconds));
            return backoff + Jitter();
        }

        private static TimeSpan Jitter() =>
            TimeSpan.FromMilliseconds(Random.Shared.Next(0, 100));

        private static bool IsTransientStatusCode(HttpStatusCode statusCode) =>
            statusCode == HttpStatusCode.TooManyRequests ||
            (int)statusCode >= 500;

        private static bool IsTransientException(Exception exception, CancellationToken cancellationToken) =>
            exception is HttpRequestException ||
            exception is TimeoutException ||
            exception is TaskCanceledException && !cancellationToken.IsCancellationRequested;
    }

    private sealed record CircuitSample(DateTimeOffset ObservedAt, bool Success);

    private sealed record BufferedHttpRequestSnapshot(
        HttpMethod Method,
        Uri? RequestUri,
        Version Version,
        HttpVersionPolicy VersionPolicy,
        IReadOnlyList<KeyValuePair<string, IEnumerable<string>>> Headers,
        byte[]? Content,
        IReadOnlyList<KeyValuePair<string, IEnumerable<string>>> ContentHeaders)
    {
        public static async Task<BufferedHttpRequestSnapshot> CreateAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            byte[]? content = null;
            var contentHeaders = Array.Empty<KeyValuePair<string, IEnumerable<string>>>();
            if (request.Content is not null)
            {
                content = await request.Content.ReadAsByteArrayAsync(cancellationToken);
                contentHeaders = request.Content.Headers.ToArray();
            }

            return new BufferedHttpRequestSnapshot(
                request.Method,
                request.RequestUri,
                request.Version,
                request.VersionPolicy,
                request.Headers.ToArray(),
                content,
                contentHeaders);
        }

        public HttpRequestMessage CreateRequest()
        {
            var request = new HttpRequestMessage(Method, RequestUri)
            {
                Version = Version,
                VersionPolicy = VersionPolicy,
            };

            foreach (var header in Headers)
            {
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            if (Content is null)
            {
                return request;
            }

            request.Content = new ByteArrayContent(Content);
            foreach (var header in ContentHeaders)
            {
                request.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            return request;
        }
    }
}
