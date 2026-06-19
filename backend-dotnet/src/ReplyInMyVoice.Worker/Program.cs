using ReplyInMyVoice.Infrastructure;
using ReplyInMyVoice.Worker;

var builder = Host.CreateApplicationBuilder(args);
var inProcessRewriteWorkerEnabled = bool.TryParse(builder.Configuration["ENABLE_INPROC_REWRITE_WORKER"], out var enableInProcRewriteWorker)
    && enableInProcRewriteWorker;

builder.Services.AddApplicationInsightsTelemetryWorkerService();
builder.Services.AddActivitySourceTelemetry(builder.Configuration, builder.Environment.EnvironmentName);
builder.Services.AddReplyInMyVoiceInfrastructure(
    builder.Configuration,
    builder.Environment.EnvironmentName,
    requireServiceBusConsumer: inProcessRewriteWorkerEnabled);
builder.Services.AddHostedService<OutboxDispatcherWorker>();
builder.Services.AddHostedService<ExpiredReservationCleanupWorker>();

if (inProcessRewriteWorkerEnabled)
{
    builder.Services.AddHostedService<ServiceBusRewriteWorker>();
}

var host = builder.Build();
host.Run();
