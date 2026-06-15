using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace ReplyInMyVoice.Infrastructure.Configuration;

public sealed class RewriteEngineOptions
{
    public const string SectionName = "RewriteEngine";

    public const string ModelBaseUrlKey = "OPENAI_BASE_URL";
    public const string ModelKey = "OPENAI_MODEL";
    public const string MidWriterModelKey = "OPENAI_MODEL_MID_WRITER";
    public const string ModelTimeoutSecondsKey = "OPENAI_TIMEOUT_SEC";
    public const string SignalTimeoutSecondsKey = "WRITING_SIGNAL_TIMEOUT_SEC";
    public const string AiSignalTargetKey = "AI_SIGNAL_TARGET";
    public const string MaxAttemptsKey = "REWRITE_MAX_ATTEMPTS";
    public const string TotalBudgetSecondsKey = "TOTAL_REWRITE_BUDGET_SEC";

    /// <summary>Absolute http(s) base URI for the OpenAI-compatible model API.</summary>
    public string ModelBaseUrl { get; set; } = "https://api.openai.com/v1";

    /// <summary>Model name used by the mid-writer route.</summary>
    public string Model { get; set; } = "gpt-4o-mini";

    /// <summary>Model call timeout in seconds. Valid range: 1 or greater.</summary>
    public int ModelTimeoutSeconds { get; set; } = 60;

    /// <summary>Writing signal call timeout in seconds. Valid range: 1 or greater.</summary>
    public int SignalTimeoutSeconds { get; set; } = 10;

    /// <summary>Target AI-signal percentage. Valid range: 0 through 100.</summary>
    public int AiSignalTarget { get; set; } = 20;

    /// <summary>Maximum bounded rewrite attempts. Valid range: 1 through 50.</summary>
    public int MaxAttempts { get; set; } = 10;

    /// <summary>Total rewrite budget in seconds. Valid range: 0 or greater, where 0 is unlimited.</summary>
    public int TotalBudgetSeconds { get; set; } = 180;

    public static RewriteEngineOptions FromConfiguration(IConfiguration configuration)
    {
        var options = new RewriteEngineOptions();
        configuration.GetSection(SectionName).Bind(options);
        options.ApplyEnvironmentKeyOverrides(configuration);
        return options;
    }

    public void ApplyEnvironmentKeyOverrides(IConfiguration configuration)
    {
        if (!string.IsNullOrWhiteSpace(configuration[ModelBaseUrlKey]))
        {
            ModelBaseUrl = configuration[ModelBaseUrlKey]!.Trim();
        }

        var configuredModel = configuration[MidWriterModelKey] ?? configuration[ModelKey];
        if (!string.IsNullOrWhiteSpace(configuredModel))
        {
            Model = configuredModel.Trim();
        }

        ApplyInteger(configuration, ModelTimeoutSecondsKey, value => ModelTimeoutSeconds = value);
        ApplyInteger(configuration, SignalTimeoutSecondsKey, value => SignalTimeoutSeconds = value);
        ApplyInteger(configuration, AiSignalTargetKey, value => AiSignalTarget = value);
        ApplyInteger(configuration, MaxAttemptsKey, value => MaxAttempts = value);
        ApplyInteger(configuration, TotalBudgetSecondsKey, value => TotalBudgetSeconds = value);
    }

    private static void ApplyInteger(
        IConfiguration configuration,
        string key,
        Action<int> apply)
    {
        if (int.TryParse(
                configuration[key],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var parsed))
        {
            apply(parsed);
        }
    }
}
