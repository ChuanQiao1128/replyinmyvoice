using ReplyInMyVoice.Infrastructure;
using ReplyInMyVoice.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddApplicationInsightsTelemetryWorkerService();
builder.Services.AddReplyInMyVoiceInfrastructure(builder.Configuration);
builder.Services.AddHostedService<ServiceBusRewriteWorker>();

var host = builder.Build();
host.Run();
