using Azure.Messaging.ServiceBus;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Infrastructure.Configuration;
using ReplyInMyVoice.Infrastructure.Data;
using ReplyInMyVoice.Infrastructure.Services;
using Stripe;
using System.Net;
using System.Text.Json.Serialization;

namespace ReplyInMyVoice.Functions.Functions;

public sealed class HealthFunction
{
    private const int DefaultOutboxBacklogMinutes = 10;
    private const int DefaultFailedStripeEventsThreshold = 0;
    private const int DefaultStripeLastProcessedMaxAgeMinutes = 0;
    private const int DefaultOutboxBacklogThreshold = 0;
    private const int DefaultStuckReservationsThreshold = 0;

    private readonly AppDbContext db;
    private readonly IConfiguration configuration;
    private readonly ServiceBusSender? serviceBusSender;
    private readonly IStripeAuthProbe stripeAuthProbe;

    public HealthFunction(
        AppDbContext db,
        IConfiguration configuration,
        ServiceBusSender? serviceBusSender = null,
        IStripeAuthProbe? stripeAuthProbe = null)
    {
        this.db = db;
        this.configuration = configuration;
        this.serviceBusSender = serviceBusSender;
        this.stripeAuthProbe = stripeAuthProbe ?? new StripeBillingClient();
    }

    [Function("Health")]
    public IActionResult Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")]
        HttpRequest request)
    {
        return new OkObjectResult(new { ok = true, service = "replyinmyvoice-functions" });
    }

    [Function("DatabaseHealth")]
    public async Task<IActionResult> DatabaseHealth(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health/db")]
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        var canConnect = await db.Database.CanConnectAsync(cancellationToken);
        return canConnect
            ? new OkObjectResult(new { ok = true, database = "azure-sql" })
            : new ObjectResult(new { ok = false, database = "azure-sql" })
            {
                StatusCode = StatusCodes.Status500InternalServerError,
            };
    }

    [Function("ReadinessHealth")]
    public async Task<IActionResult> ReadinessHealth(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health/ready")]
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var outboxBacklogMinutes = ReadPositiveInt("Health:OutboxBacklogMinutes", DefaultOutboxBacklogMinutes);
        var failedStripeEventsThreshold = ReadNonNegativeInt(
            "Health:FailedStripeEventsThreshold",
            DefaultFailedStripeEventsThreshold);
        var stripeLastProcessedMaxAgeMinutes = ReadNonNegativeInt(
            "Health:StripeLastProcessedMaxAgeMinutes",
            DefaultStripeLastProcessedMaxAgeMinutes);
        var outboxBacklogThreshold = ReadNonNegativeInt(
            "Health:OutboxBacklogThreshold",
            DefaultOutboxBacklogThreshold);
        var stuckReservationsThreshold = ReadNonNegativeInt(
            "Health:StuckReservationsThreshold",
            DefaultStuckReservationsThreshold);

        var database = await CheckDatabaseAsync(cancellationToken);
        var serviceBus = CheckServiceBus();
        var stripeAuth = await CheckStripeAuthAsync(cancellationToken);
        var failedStripeEvents = database.Ok
            ? await CheckFailedStripeEventsAsync(failedStripeEventsThreshold, cancellationToken)
            : CountReadinessCheck.Unavailable("database_unavailable", failedStripeEventsThreshold);
        var lastProcessedStripeEvent = database.Ok
            ? await CheckLastProcessedStripeEventAsync(now, stripeLastProcessedMaxAgeMinutes, cancellationToken)
            : LastProcessedStripeEventReadinessCheck.Unavailable(
                "database_unavailable",
                stripeLastProcessedMaxAgeMinutes);
        var outboxBacklog = database.Ok
            ? await CheckOutboxBacklogAsync(now, outboxBacklogMinutes, outboxBacklogThreshold, cancellationToken)
            : CountReadinessCheck.Unavailable("database_unavailable", outboxBacklogThreshold);
        var stuckReservations = database.Ok
            ? await CheckStuckReservationsAsync(now, stuckReservationsThreshold, cancellationToken)
            : CountReadinessCheck.Unavailable("database_unavailable", stuckReservationsThreshold);

        var checks = new ReadinessChecks(
            database,
            serviceBus,
            stripeAuth,
            failedStripeEvents,
            lastProcessedStripeEvent,
            outboxBacklog,
            stuckReservations);
        var ok = checks.Database.Ok &&
            checks.ServiceBus.Ok &&
            checks.StripeAuth.Ok &&
            checks.FailedStripeEvents.Ok &&
            checks.LastProcessedStripeEvent.Ok &&
            checks.OutboxBacklog.Ok &&
            checks.StuckReservations.Ok;
        var response = new ReadinessResponse(
            ok,
            ok ? "ready" : "degraded",
            now,
            checks);

        return ok
            ? new OkObjectResult(response)
            : new ObjectResult(response)
            {
                StatusCode = StatusCodes.Status503ServiceUnavailable,
            };
    }

    private async Task<DatabaseReadinessCheck> CheckDatabaseAsync(CancellationToken cancellationToken)
    {
        try
        {
            var canConnect = await db.Database.CanConnectAsync(cancellationToken);
            return new DatabaseReadinessCheck(canConnect, canConnect, canConnect ? null : "cannot_connect");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new DatabaseReadinessCheck(false, false, ex.GetType().Name);
        }
    }

    private ServiceBusReadinessCheck CheckServiceBus()
    {
        var connectionString = configuration.GetConnectionString("ServiceBus")
            ?? configuration["ServiceBus"]
            ?? configuration["SERVICEBUS_CONNECTION_STRING"]
            ?? configuration["AZURE_SERVICE_BUS_CONNECTION_STRING"];
        var queueName = configuration["ServiceBus:QueueName"]
            ?? configuration["SERVICEBUS_QUEUE_NAME"]
            ?? configuration["AZURE_SERVICE_BUS_QUEUE"]
            ?? "rewrite-jobs";
        var managedIdentityConfigured = ManagedIdentityConfiguration.IsEnabled(configuration) &&
            ManagedIdentityConfiguration.ResolveServiceBusFullyQualifiedNamespace(configuration) is not null;
        var configured = managedIdentityConfigured || !string.IsNullOrWhiteSpace(connectionString);
        var senderResolved = serviceBusSender is not null;
        var ok = configured && senderResolved;
        var authMode = managedIdentityConfigured
            ? "managed_identity"
            : string.IsNullOrWhiteSpace(connectionString) ? "none" : "connection_string";

        return new ServiceBusReadinessCheck(
            ok,
            configured,
            senderResolved,
            queueName,
            authMode,
            ok ? null : configured ? "sender_unavailable" : "not_configured");
    }

    private async Task<StripeAuthReadinessCheck> CheckStripeAuthAsync(CancellationToken cancellationToken)
    {
        const string authMode = "secret_key";

        try
        {
            StripeBillingService.EnsureStripeApiVersionPinned();
            var stripeClient = new StripeClient(GetRequiredConfiguration("STRIPE_SECRET_KEY"));
            var authenticated = await stripeAuthProbe.VerifyAuthenticatedAsync(stripeClient, cancellationToken);
            return new StripeAuthReadinessCheck(
                authenticated,
                authMode,
                authenticated ? null : "invalid_request");
        }
        catch (StripeException ex) when (
            ex.HttpStatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return new StripeAuthReadinessCheck(false, authMode, "auth_failed");
        }
        catch (StripeException ex) when (ex.HttpStatusCode == HttpStatusCode.BadRequest)
        {
            return new StripeAuthReadinessCheck(false, authMode, "invalid_request");
        }
        catch (StripeException)
        {
            return new StripeAuthReadinessCheck(false, authMode, "provider_error");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new StripeAuthReadinessCheck(false, authMode, ex.GetType().Name);
        }
    }

    private async Task<CountReadinessCheck> CheckFailedStripeEventsAsync(
        int threshold,
        CancellationToken cancellationToken)
    {
        try
        {
            var count = await CountFailedStripeEventsAsync(cancellationToken);
            return CountReadinessCheck.FromCount(count, threshold);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return CountReadinessCheck.Unavailable(ex.GetType().Name, threshold);
        }
    }

    private async Task<LastProcessedStripeEventReadinessCheck> CheckLastProcessedStripeEventAsync(
        DateTimeOffset now,
        int maxAgeMinutes,
        CancellationToken cancellationToken)
    {
        try
        {
            var lastProcessedAt = await GetLastProcessedStripeEventAsync(cancellationToken);
            return LastProcessedStripeEventReadinessCheck.FromLastProcessed(now, lastProcessedAt, maxAgeMinutes);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return LastProcessedStripeEventReadinessCheck.Unavailable(ex.GetType().Name, maxAgeMinutes);
        }
    }

    private async Task<CountReadinessCheck> CheckOutboxBacklogAsync(
        DateTimeOffset now,
        int olderThanMinutes,
        int threshold,
        CancellationToken cancellationToken)
    {
        try
        {
            var cutoff = now.AddMinutes(-olderThanMinutes);
            var count = await CountOutboxBacklogAsync(cutoff, cancellationToken);
            return CountReadinessCheck.FromCount(count, threshold, olderThanMinutes);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return CountReadinessCheck.Unavailable(ex.GetType().Name, threshold, olderThanMinutes);
        }
    }

    private async Task<CountReadinessCheck> CheckStuckReservationsAsync(
        DateTimeOffset now,
        int threshold,
        CancellationToken cancellationToken)
    {
        try
        {
            var count = await CountStuckReservationsAsync(now, cancellationToken);
            return CountReadinessCheck.FromCount(count, threshold);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return CountReadinessCheck.Unavailable(ex.GetType().Name, threshold);
        }
    }

    private async Task<int> CountFailedStripeEventsAsync(CancellationToken cancellationToken) =>
        await db.StripeEvents
            .AsNoTracking()
            .CountAsync(x => x.Status == StripeEventStatus.Failed, cancellationToken);

    private async Task<DateTimeOffset?> GetLastProcessedStripeEventAsync(CancellationToken cancellationToken)
    {
        if (IsSqlite())
        {
            var processedEvents = await db.StripeEvents
                .AsNoTracking()
                .Where(x => x.Status == StripeEventStatus.Processed)
                .Select(x => new { x.ProcessedAt, x.CreatedAt })
                .ToListAsync(cancellationToken);

            return processedEvents.Count == 0
                ? null
                : processedEvents.Max(x => x.ProcessedAt ?? x.CreatedAt);
        }

        return await db.StripeEvents
            .AsNoTracking()
            .Where(x => x.Status == StripeEventStatus.Processed)
            .MaxAsync(x => (DateTimeOffset?)(x.ProcessedAt ?? x.CreatedAt), cancellationToken);
    }

    private async Task<int> CountOutboxBacklogAsync(
        DateTimeOffset cutoff,
        CancellationToken cancellationToken)
    {
        if (IsSqlite())
        {
            var messages = await db.OutboxMessages
                .AsNoTracking()
                .Select(x => new { x.Status, x.CreatedAt })
                .ToListAsync(cancellationToken);

            return messages.Count(x =>
                x.Status == OutboxMessageStatus.Failed ||
                ((x.Status == OutboxMessageStatus.Pending || x.Status == OutboxMessageStatus.Processing) &&
                    x.CreatedAt <= cutoff));
        }

        return await db.OutboxMessages
            .AsNoTracking()
            .CountAsync(
                x => x.Status == OutboxMessageStatus.Failed ||
                    ((x.Status == OutboxMessageStatus.Pending || x.Status == OutboxMessageStatus.Processing) &&
                        x.CreatedAt <= cutoff),
                cancellationToken);
    }

    private async Task<int> CountStuckReservationsAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (IsSqlite())
        {
            var reservations = await db.UsageReservations
                .AsNoTracking()
                .Where(x => x.Status == UsageReservationStatus.Pending)
                .Select(x => new { x.ExpiresAt })
                .ToListAsync(cancellationToken);

            return reservations.Count(x => x.ExpiresAt <= now);
        }

        return await db.UsageReservations
            .AsNoTracking()
            .CountAsync(
                x => x.Status == UsageReservationStatus.Pending && x.ExpiresAt <= now,
                cancellationToken);
    }

    private bool IsSqlite() =>
        db.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true;

    private int ReadPositiveInt(string key, int fallback)
    {
        var value = configuration[key] ?? configuration[key.Replace(':', '_')];
        return int.TryParse(value, out var parsed) && parsed > 0
            ? parsed
            : fallback;
    }

    private int ReadNonNegativeInt(string key, int fallback)
    {
        var value = configuration[key] ?? configuration[key.Replace(':', '_')];
        return int.TryParse(value, out var parsed) && parsed >= 0
            ? parsed
            : fallback;
    }

    private string GetRequiredConfiguration(string key) =>
        configuration[key] ?? throw new InvalidOperationException($"{key}_missing");

    public sealed record ReadinessResponse(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("checkedAt")] DateTimeOffset CheckedAt,
        [property: JsonPropertyName("checks")] ReadinessChecks Checks);

    public sealed record ReadinessChecks(
        [property: JsonPropertyName("database")] DatabaseReadinessCheck Database,
        [property: JsonPropertyName("serviceBus")] ServiceBusReadinessCheck ServiceBus,
        [property: JsonPropertyName("stripeAuth")] StripeAuthReadinessCheck StripeAuth,
        [property: JsonPropertyName("failedStripeEvents")] CountReadinessCheck FailedStripeEvents,
        [property: JsonPropertyName("lastProcessedStripeEvent")]
        LastProcessedStripeEventReadinessCheck LastProcessedStripeEvent,
        [property: JsonPropertyName("outboxBacklog")] CountReadinessCheck OutboxBacklog,
        [property: JsonPropertyName("stuckReservations")] CountReadinessCheck StuckReservations);

    public sealed record DatabaseReadinessCheck(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("canConnect")] bool CanConnect,
        [property: JsonPropertyName("error")] string? Error);

    public sealed record ServiceBusReadinessCheck(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("configured")] bool Configured,
        [property: JsonPropertyName("senderResolved")] bool SenderResolved,
        [property: JsonPropertyName("queueName")] string QueueName,
        [property: JsonPropertyName("authMode")] string AuthMode,
        [property: JsonPropertyName("error")] string? Error);

    public sealed record StripeAuthReadinessCheck(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("authMode")] string AuthMode,
        [property: JsonPropertyName("error")] string? Error);

    public sealed record CountReadinessCheck(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("count")] int Count,
        [property: JsonPropertyName("threshold")] int Threshold,
        [property: JsonPropertyName("olderThanMinutes")] int? OlderThanMinutes,
        [property: JsonPropertyName("error")] string? Error)
    {
        public static CountReadinessCheck FromCount(
            int count,
            int threshold,
            int? olderThanMinutes = null) =>
            new(count <= threshold, count, threshold, olderThanMinutes, null);

        public static CountReadinessCheck Unavailable(
            string error,
            int threshold,
            int? olderThanMinutes = null) =>
            new(false, 0, threshold, olderThanMinutes, error);
    }

    public sealed record LastProcessedStripeEventReadinessCheck(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("lastProcessedAt")] DateTimeOffset? LastProcessedAt,
        [property: JsonPropertyName("ageSeconds")] long? AgeSeconds,
        [property: JsonPropertyName("maxAgeMinutes")] int MaxAgeMinutes,
        [property: JsonPropertyName("error")] string? Error)
    {
        public static LastProcessedStripeEventReadinessCheck FromLastProcessed(
            DateTimeOffset now,
            DateTimeOffset? lastProcessedAt,
            int maxAgeMinutes)
        {
            if (lastProcessedAt is null)
            {
                return new(
                    maxAgeMinutes == 0,
                    null,
                    null,
                    maxAgeMinutes,
                    maxAgeMinutes == 0 ? null : "no_processed_events");
            }

            var ageSeconds = Math.Max(0, (long)Math.Floor((now - lastProcessedAt.Value).TotalSeconds));
            var maxAgeSeconds = maxAgeMinutes * 60L;
            var ok = maxAgeMinutes == 0 || ageSeconds <= maxAgeSeconds;
            return new(ok, lastProcessedAt, ageSeconds, maxAgeMinutes, ok ? null : "stale");
        }

        public static LastProcessedStripeEventReadinessCheck Unavailable(
            string error,
            int maxAgeMinutes) =>
            new(false, null, null, maxAgeMinutes, error);
    }
}
