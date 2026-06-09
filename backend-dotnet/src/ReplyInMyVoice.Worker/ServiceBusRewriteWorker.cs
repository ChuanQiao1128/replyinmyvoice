using System.Text.Json;
using Azure.Messaging.ServiceBus;
using ReplyInMyVoice.Application.UseCases.RewriteJob;
using ReplyInMyVoice.Domain.Contracts;

namespace ReplyInMyVoice.Worker;

public sealed class ServiceBusRewriteWorker(
    IConfiguration configuration,
    IServiceScopeFactory scopeFactory,
    ILogger<ServiceBusRewriteWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var connectionString = configuration.GetConnectionString("ServiceBus")
            ?? configuration["SERVICEBUS_CONNECTION_STRING"]
            ?? configuration["AZURE_SERVICE_BUS_CONNECTION_STRING"];

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            logger.LogWarning("Service Bus connection string is not configured; worker is idle.");
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
            return;
        }

        var queueName = configuration["ServiceBus:QueueName"]
            ?? configuration["SERVICEBUS_QUEUE_NAME"]
            ?? configuration["AZURE_SERVICE_BUS_QUEUE"]
            ?? "rewrite-jobs";

        await using var client = new ServiceBusClient(connectionString);
        await using var processor = client.CreateProcessor(queueName, new ServiceBusProcessorOptions
        {
            MaxConcurrentCalls = 4,
            AutoCompleteMessages = false,
            MaxAutoLockRenewalDuration = TimeSpan.FromMinutes(10),
        });

        processor.ProcessMessageAsync += ProcessMessageAsync;
        processor.ProcessErrorAsync += args =>
        {
            logger.LogError(args.Exception, "Service Bus processing error from {Source}", args.ErrorSource);
            return Task.CompletedTask;
        };

        await processor.StartProcessingAsync(stoppingToken);

        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
        finally
        {
            await processor.StopProcessingAsync(CancellationToken.None);
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

        using var scope = scopeFactory.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<ProcessRewriteJobHandler>();

        try
        {
            await handler.HandleAsync(new ProcessRewriteJobCommand(job.AttemptId), args.CancellationToken);
            await args.CompleteMessageAsync(args.Message);
        }
        catch (RewriteJobAttemptNotFoundException ex)
        {
            logger.LogWarning(ex, "Dead-lettering rewrite job for missing attempt {AttemptId}", job.AttemptId);
            await args.DeadLetterMessageAsync(args.Message, "attempt_not_found", ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Rewrite job failed for attempt {AttemptId}", job.AttemptId);
            throw;
        }
    }
}
