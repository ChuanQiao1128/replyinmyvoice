using System.Diagnostics;
using Microsoft.Extensions.Configuration;

namespace ReplyInMyVoice.Infrastructure.Configuration;

public static class KeyVaultConfigurationExtensions
{
    public static IConfigurationBuilder AddReplyInMyVoiceKeyVault(
        this IConfigurationBuilder builder,
        IConfiguration configuration)
    {
        if (!ShouldAttach(configuration))
        {
            return builder;
        }

        try
        {
            // TODO: Attach Azure Key Vault provider when Azure.Extensions.AspNetCore.Configuration.Secrets
            // is available in the offline restore set.
            return builder;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Trace.TraceWarning(
                "ReplyInMyVoice Key Vault configuration source was not added: {0}",
                ex.GetType().Name);
            return builder;
        }
    }

    public static bool ShouldAttach(IConfiguration configuration) =>
        ManagedIdentityConfiguration.IsEnabled(configuration) &&
        ResolveVaultUri(configuration) is not null;

    public static Uri? ResolveVaultUri(IConfiguration configuration)
    {
        var configured = configuration["KEY_VAULT_URI"] ?? configuration["AZURE_KEY_VAULT_URI"];
        if (string.IsNullOrWhiteSpace(configured) ||
            !Uri.TryCreate(configured.Trim(), UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps)
        {
            return null;
        }

        return uri;
    }
}
