using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ReplyInMyVoice.Functions.Http;
using ReplyInMyVoice.Infrastructure;

var builder = FunctionsApplication.CreateBuilder(args);

// Read the build-stamped version.generated.json from next to the assembly. A bare relative
// path resolves against ContentRoot/CWD, which on Azure Functions Linux (WEBSITE_RUN_FROM_PACKAGE)
// is NOT where CopyToOutputDirectory places the file — so /api/version reported "unknown" even
// though the file shipped in the package. AppContext.BaseDirectory is the assembly dir = file dir.
builder.Configuration.AddJsonFile(
    Path.Combine(AppContext.BaseDirectory, "version.generated.json"),
    optional: true,
    reloadOnChange: false);

builder.ConfigureFunctionsWebApplication();
builder.UseMiddleware<HttpHardeningMiddleware>();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();
builder.Services.AddActivitySourceTelemetry(builder.Configuration, builder.Environment.EnvironmentName);

builder.Services.AddReplyInMyVoiceInfrastructure(
    builder.Configuration,
    builder.Environment.EnvironmentName,
    requireServiceBusConsumer: true);

builder.Build().Run();
