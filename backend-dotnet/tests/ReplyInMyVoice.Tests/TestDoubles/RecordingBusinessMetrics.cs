using ReplyInMyVoice.Application.Abstractions;

namespace ReplyInMyVoice.Tests.TestDoubles;

public sealed class RecordingBusinessMetrics : IBusinessMetrics
{
    private readonly List<BusinessMetricRecord> _records = [];

    public IReadOnlyList<BusinessMetricRecord> Records => _records;

    public void Record(string metricName, double value) =>
        _records.Add(new BusinessMetricRecord(metricName, value, null, null));

    public void Record(string metricName, double value, string dimensionName, string dimensionValue) =>
        _records.Add(new BusinessMetricRecord(metricName, value, dimensionName, dimensionValue));

    public void Record(
        string metricName,
        double value,
        string firstDimensionName,
        string firstDimensionValue,
        string secondDimensionName,
        string secondDimensionValue) =>
        _records.Add(new BusinessMetricRecord(
            metricName,
            value,
            firstDimensionName,
            firstDimensionValue,
            secondDimensionName,
            secondDimensionValue));
}

public sealed record BusinessMetricRecord(
    string Name,
    double Value,
    string? DimensionName,
    string? DimensionValue,
    string? SecondDimensionName = null,
    string? SecondDimensionValue = null);
