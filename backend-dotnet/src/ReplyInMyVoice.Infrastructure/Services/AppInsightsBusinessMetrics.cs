using Microsoft.ApplicationInsights;
using ReplyInMyVoice.Application.Abstractions;

namespace ReplyInMyVoice.Infrastructure.Services;

public sealed class AppInsightsBusinessMetrics(TelemetryClient telemetryClient) : IBusinessMetrics
{
    public void Record(string metricName, double value)
    {
        try
        {
            telemetryClient.GetMetric(metricName).TrackValue(value);
        }
        catch
        {
        }
    }

    public void Record(string metricName, double value, string dimensionName, string dimensionValue)
    {
        try
        {
            telemetryClient.GetMetric(metricName, dimensionName).TrackValue(value, dimensionValue);
        }
        catch
        {
        }
    }

    public void Record(
        string metricName,
        double value,
        string firstDimensionName,
        string firstDimensionValue,
        string secondDimensionName,
        string secondDimensionValue)
    {
        try
        {
            telemetryClient
                .GetMetric(metricName, firstDimensionName, secondDimensionName)
                .TrackValue(value, firstDimensionValue, secondDimensionValue);
        }
        catch
        {
        }
    }
}
