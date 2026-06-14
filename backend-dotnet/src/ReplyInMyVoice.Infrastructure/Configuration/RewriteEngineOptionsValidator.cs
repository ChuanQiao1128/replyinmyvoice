using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace ReplyInMyVoice.Infrastructure.Configuration;

public sealed class RewriteEngineOptionsValidator(
    IConfiguration configuration,
    string? environmentName) : IValidateOptions<RewriteEngineOptions>
{
    public ValidateOptionsResult Validate(string? name, RewriteEngineOptions options)
    {
        if (IsDevelopmentOrTesting(environmentName))
        {
            return ValidateOptionsResult.Success;
        }

        var failures = new List<string>();

        ValidateBaseUrl(options, failures);
        ValidateInteger(
            RewriteEngineOptions.ModelTimeoutSecondsKey,
            options.ModelTimeoutSeconds,
            value => value >= 1,
            "must be greater than or equal to 1.",
            failures);
        ValidateInteger(
            RewriteEngineOptions.SignalTimeoutSecondsKey,
            options.SignalTimeoutSeconds,
            value => value >= 1,
            "must be greater than or equal to 1.",
            failures);
        ValidateInteger(
            RewriteEngineOptions.AiSignalTargetKey,
            options.AiSignalTarget,
            value => value is >= 0 and <= 100,
            "must be between 0 and 100.",
            failures);
        ValidateInteger(
            RewriteEngineOptions.MaxAttemptsKey,
            options.MaxAttempts,
            value => value is >= 1 and <= 50,
            "must be between 1 and 50.",
            failures);
        ValidateInteger(
            RewriteEngineOptions.TotalBudgetSecondsKey,
            options.TotalBudgetSeconds,
            value => value >= 0,
            "must be greater than or equal to 0.",
            failures);

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static bool IsDevelopmentOrTesting(string? environmentName) =>
        string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(environmentName, "Testing", StringComparison.OrdinalIgnoreCase);

    private static void ValidateBaseUrl(
        RewriteEngineOptions options,
        List<string> failures)
    {
        if (!Uri.TryCreate(options.ModelBaseUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            failures.Add($"{RewriteEngineOptions.ModelBaseUrlKey} must be an absolute http(s) URI.");
        }
    }

    private void ValidateInteger(
        string key,
        int configuredValue,
        Func<int, bool> isValid,
        string message,
        List<string> failures)
    {
        var raw = configuration[key];
        if (!string.IsNullOrWhiteSpace(raw) &&
            !int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out configuredValue))
        {
            failures.Add($"{key} must be an integer.");
            return;
        }

        if (!isValid(configuredValue))
        {
            failures.Add($"{key} {message}");
        }
    }
}
