using System.Diagnostics;
using System.Text.Json;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using ReplyInMyVoice.Application.UseCases.RewriteJob;
using ReplyInMyVoice.Domain.Contracts;
using ReplyInMyVoice.Infrastructure.Configuration;

namespace ReplyInMyVoice.Worker;

public sealed class ServiceBusRewriteWorker : BackgroundService
{
    private static readonly TimeSpan GracefulDrainTimeout = TimeSpan.FromSeconds(60);
    private readonly IConfiguration _configuration;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ServiceBusRewriteWorker> _logger;
    private readonly IRewriteServiceBusProcessorFactory _processorFactory;
    private IRewriteServiceBusProcessor? _processor;

    public ServiceBusRewriteWorker(
        IConfiguration configuration,
        IServiceScopeFactory scopeFactory,
        ILogger<ServiceBusRewriteWorker> logger)
        : this(configuration, scopeFactory, logger, new AzureRewriteServiceBusProcessorFactory())
    {
    }

    internal ServiceBusRewriteWorker(
        IConfiguration configuration,
        IServiceScopeFactory scopeFactory,
        ILogger<ServiceBusRewriteWorker> logger,
        IRewriteServiceBusProcessorFactory processorFactory)
    {
        _configuration = configuration;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _processorFactory = processorFactory;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        _logger.LogDebug("Starting graceful drain for Service Bus rewrite worker.");

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(GracefulDrainTimeout);

        try
        {
            var processor = Volatile.Read(ref _processor);
            if (processor is not null)
            {
                await processor.StopProcessingAsync(timeoutCts.Token);
            }

            await base.StopAsync(timeoutCts.Token);
            _logger.LogInformation("Graceful drain completed within {ElapsedMilliseconds} ms.", stopwatch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && timeoutCts.IsCancellationRequested)
        {
            _logger.LogWarning(
                "Graceful drain timed out after {TimeoutMilliseconds} ms.",
                GracefulDrainTimeout.TotalMilliseconds);
            try
            {
                await base.StopAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
            }
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var connectionString = _configuration.GetConnectionString("ServiceBus")
            ?? _configuration["SERVICEBUS_CONNECTION_STRING"]
            ?? _configuration["AZURE_SERVICE_BUS_CONNECTION_STRING"];
        var managedIdentityEnabled = ManagedIdentityConfiguration.IsEnabled(_configuration);
        var serviceBusNamespace = ManagedIdentityConfiguration.ResolveServiceBusFullyQualifiedNamespace(_configuration);

        if (string.IsNullOrWhiteSpace(connectionString) &&
            (!managedIdentityEnabled || serviceBusNamespace is null))
        {
            _logger.LogWarning("Service Bus is not configured (connection string or managed identity); worker is idle.");
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
            return;
        }

        await using var processor = _processorFactory.CreateProcessor(
            _configuration,
            ProcessMessageAsync,
            args =>
            {
                _logger.LogError(args.Exception, "Service Bus processing error from {Source}", args.ErrorSource);
                return Task.CompletedTask;
            });
        Volatile.Write(ref _processor, processor);

        try
        {
            await processor.StartProcessingAsync(stoppingToken);
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
        finally
        {
            Volatile.Write(ref _processor, null);
        }
    }

    private async Task ProcessMessageAsync(ProcessMessageEventArgs args)
    {
        RewriteJob? job;
        try
        {
            job = JsonSerializer.Deserialize<RewriteJob>(args.Message.Body);
            if (job is null)
            {
                await args.DeadLetterMessageAsync(args.Message, "invalid_job", "Message body did not contain a rewrite job.");
                return;
            }

            if (job.AttemptId == Guid.Empty)
            {
                await args.DeadLetterMessageAsync(args.Message, "invalid_job", "Message body did not contain a valid attempt id.");
                return;
            }
        }
        catch (JsonException ex)
        {
            await args.DeadLetterMessageAsync(args.Message, "invalid_json", ex.Message);
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<ProcessRewriteJobHandler>();
        using var activity = StartConsumerActivity(job);
        using var logScope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = job.CorrelationId,
            ["AttemptId"] = job.AttemptId,
        });

        try
        {
            await handler.HandleAsync(new ProcessRewriteJobCommand(job.AttemptId), args.CancellationToken);
            await args.CompleteMessageAsync(args.Message);
        }
        catch (RewriteJobAttemptNotFoundException ex)
        {
            _logger.LogWarning(ex, "Dead-lettering rewrite job for missing attempt {AttemptId}", job.AttemptId);
            await args.DeadLetterMessageAsync(args.Message, "attempt_not_found", ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Rewrite job failed for attempt {AttemptId}", job.AttemptId);
            throw;
        }
    }

    private static Activity? StartConsumerActivity(RewriteJob job)
    {
        if (string.IsNullOrWhiteSpace(job.Traceparent) ||
            !ActivityContext.TryParse(job.Traceparent, null, out _))
        {
            return null;
        }

        var activity = new Activity("ProcessRewriteJob");
        activity.SetParentId(job.Traceparent);
        activity.SetTag("correlation_id", job.CorrelationId);
        activity.SetTag("attempt_id", job.AttemptId);
        return activity.Start();
    }
}

internal interface IRewriteServiceBusProcessorFactory
{
    IRewriteServiceBusProcessor CreateProcessor(
        IConfiguration configuration,
        Func<ProcessMessageEventArgs, Task> processMessageAsync,
        Func<ProcessErrorEventArgs, Task> processErrorAsync);
}

internal interface IRewriteServiceBusProcessor : IAsyncDisposable
{
    event Func<ProcessMessageEventArgs, Task>? ProcessMessageAsync;
    event Func<ProcessErrorEventArgs, Task>? ProcessErrorAsync;

    Task StartProcessingAsync(CancellationToken cancellationToken);

    Task StopProcessingAsync(CancellationToken cancellationToken);
}

internal sealed class AzureRewriteServiceBusProcessorFactory : IRewriteServiceBusProcessorFactory
{
    public IRewriteServiceBusProcessor CreateProcessor(
        IConfiguration configuration,
        Func<ProcessMessageEventArgs, Task> processMessageAsync,
        Func<ProcessErrorEventArgs, Task> processErrorAsync)
    {
        var connectionString = configuration.GetConnectionString("ServiceBus")
            ?? configuration["SERVICEBUS_CONNECTION_STRING"]
            ?? configuration["AZURE_SERVICE_BUS_CONNECTION_STRING"];
        var managedIdentityEnabled = ManagedIdentityConfiguration.IsEnabled(configuration);
        var serviceBusNamespace = ManagedIdentityConfiguration.ResolveServiceBusFullyQualifiedNamespace(configuration);
        var queueName = configuration["ServiceBus:QueueName"]
            ?? configuration["SERVICEBUS_QUEUE_NAME"]
            ?? configuration["AZURE_SERVICE_BUS_QUEUE"]
            ?? "rewrite-jobs";

        var client = managedIdentityEnabled && serviceBusNamespace is not null
            ? new ServiceBusClient(serviceBusNamespace, new DefaultAzureCredential())
            : new ServiceBusClient(connectionString);
        var processor = client.CreateProcessor(queueName, new ServiceBusProcessorOptions
        {
            MaxConcurrentCalls = 4,
            AutoCompleteMessages = false,
            MaxAutoLockRenewalDuration = TimeSpan.FromMinutes(10),
        });

        var rewriteProcessor = new AzureRewriteServiceBusProcessor(client, processor);
        rewriteProcessor.ProcessMessageAsync += processMessageAsync;
        rewriteProcessor.ProcessErrorAsync += processErrorAsync;
        return rewriteProcessor;
    }
}

internal sealed class AzureRewriteServiceBusProcessor(
    ServiceBusClient client,
    ServiceBusProcessor processor) : IRewriteServiceBusProcessor
{
    public event Func<ProcessMessageEventArgs, Task>? ProcessMessageAsync
    {
        add
        {
            if (value is not null)
            {
                processor.ProcessMessageAsync += value;
            }
        }
        remove
        {
            if (value is not null)
            {
                processor.ProcessMessageAsync -= value;
            }
        }
    }

    public event Func<ProcessErrorEventArgs, Task>? ProcessErrorAsync
    {
        add
        {
            if (value is not null)
            {
                processor.ProcessErrorAsync += value;
            }
        }
        remove
        {
            if (value is not null)
            {
                processor.ProcessErrorAsync -= value;
            }
        }
    }

    public Task StartProcessingAsync(CancellationToken cancellationToken) =>
        processor.StartProcessingAsync(cancellationToken);

    public Task StopProcessingAsync(CancellationToken cancellationToken) =>
        processor.StopProcessingAsync(cancellationToken);

    public async ValueTask DisposeAsync()
    {
        await processor.DisposeAsync();
        await client.DisposeAsync();
    }
}
