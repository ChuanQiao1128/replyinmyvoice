using ReplyInMyVoice.Infrastructure;
using ReplyInMyVoice.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddApplicationInsightsTelemetryWorkerService();
builder.Services.AddReplyInMyVoiceInfrastructure(builder.Configuration);
builder.Services.AddHostedService<OutboxDispatcherWorker>();
builder.Services.AddHostedService<ExpiredReservationCleanupWorker>();

if (bool.TryParse(builder.Configuration["ENABLE_INPROC_REWRITE_WORKER"], out var enableInProcRewriteWorker)
    && enableInProcRewriteWorker)
{
    builder.Services.AddHostedService<ServiceBusRewriteWorker>();
}

var host = builder.Build();
host.Run();
