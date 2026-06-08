using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ReplyInMyVoice.Infrastructure;

var builder = FunctionsApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("version.generated.json", optional: true, reloadOnChange: false);

builder.ConfigureFunctionsWebApplication();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

builder.Services.AddReplyInMyVoiceInfrastructure(
    builder.Configuration,
    builder.Environment.EnvironmentName,
    requireServiceBusConsumer: true);

builder.Build().Run();
