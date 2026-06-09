using System.Globalization;
using Microsoft.Extensions.Configuration;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.Common;

namespace ReplyInMyVoice.Infrastructure.Providers;

public sealed class TaxTurnoverSettingsProvider(IConfiguration configuration) : ITaxTurnoverSettingsProvider
{
    private const decimal DefaultRegistrationThresholdNzd = 60_000m;
    private const decimal DefaultWarningFraction = 0.80m;

    public TaxTurnoverSettings GetSettings() =>
        new(
            ParseNzdMinorAmount(
                configuration["GST_TURNOVER_THRESHOLD_NZD"],
                DefaultRegistrationThresholdNzd),
            ParseWarningFraction(configuration["GST_TURNOVER_WARNING_FRACTION"]));

    private static long ParseNzdMinorAmount(string? configuredValue, decimal fallbackNzd)
    {
        var value = decimal.TryParse(
            configuredValue,
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : fallbackNzd;

        if (value < 0)
        {
            value = fallbackNzd;
        }

        return (long)Math.Round(value * 100m, MidpointRounding.AwayFromZero);
    }

    private static decimal ParseWarningFraction(string? configuredValue)
    {
        if (!decimal.TryParse(
                configuredValue,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var parsed) ||
            parsed <= 0m ||
            parsed > 1m)
        {
            return DefaultWarningFraction;
        }

        return parsed;
    }
}
