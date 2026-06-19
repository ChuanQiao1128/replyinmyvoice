using Microsoft.Extensions.Configuration;

namespace ReplyInMyVoice.Infrastructure.Observability;

public sealed record DistributedTracingSettings(bool Enabled);

public static class DistributedTracingOptions
{
    public const string EnabledSettingName = "OTEL_ENABLED";

    public static bool IsEnabled(IConfiguration configuration, string? environmentName)
    {
        if (bool.TryParse(configuration[EnabledSettingName], out var enabled))
        {
            return enabled;
        }

        var resolvedEnvironmentName = environmentName
            ?? configuration["ASPNETCORE_ENVIRONMENT"]
            ?? configuration["DOTNET_ENVIRONMENT"]
            ?? configuration["AZURE_FUNCTIONS_ENVIRONMENT"];
        return string.Equals(resolvedEnvironmentName, "Production", StringComparison.OrdinalIgnoreCase);
    }
}
