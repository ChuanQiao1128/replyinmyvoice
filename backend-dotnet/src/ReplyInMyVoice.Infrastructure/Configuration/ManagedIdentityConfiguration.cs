using Microsoft.Extensions.Configuration;

namespace ReplyInMyVoice.Infrastructure.Configuration;

public static class ManagedIdentityConfiguration
{
    public static bool IsEnabled(IConfiguration configuration) =>
        bool.TryParse(configuration["USE_MANAGED_IDENTITY"], out var enabled) && enabled;

    public static string? ResolveServiceBusFullyQualifiedNamespace(IConfiguration configuration)
    {
        var configuredNamespace = configuration["ServiceBus:fullyQualifiedNamespace"]
            ?? configuration["SERVICEBUS_FULLY_QUALIFIED_NAMESPACE"];
        if (string.IsNullOrWhiteSpace(configuredNamespace))
        {
            return null;
        }

        var normalized = configuredNamespace.Trim();
        const string serviceBusScheme = "sb://";
        if (normalized.StartsWith(serviceBusScheme, StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[serviceBusScheme.Length..];
        }

        normalized = normalized.TrimEnd('/');
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}
