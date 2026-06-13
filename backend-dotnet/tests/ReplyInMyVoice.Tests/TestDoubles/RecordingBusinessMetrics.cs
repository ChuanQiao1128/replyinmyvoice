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
}

public sealed record BusinessMetricRecord(
    string Name,
    double Value,
    string? DimensionName,
    string? DimensionValue);
