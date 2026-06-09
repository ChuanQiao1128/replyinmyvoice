using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.Common;

namespace ReplyInMyVoice.Application.UseCases.ApiKey;

public sealed class GetApiUsageSeriesHandler(IApiKeyUsageRepository usage)
{
    public async Task<IReadOnlyList<ApiUsageSeriesPointDto>> HandleAsync(
        GetApiUsageSeriesQuery query,
        CancellationToken ct = default)
    {
        var boundedDays = ApiUsageWindow.ClampUsageWindowDays(query.Days);
        var today = ApiUsageWindow.ToBusinessDate(query.Now);
        var start = today.AddDays(-(boundedDays - 1));
        var rows = await usage.ListUsageRowsAsync(
            query.UserId,
            ApiUsageWindow.ToBusinessDateStartUtc(start),
            ct);
        var eligibleRows = rows
            .Where(x => x.CreatedAt <= query.Now)
            .Select(x => new ApiUsageWindow.LocalUsageRow(
                ApiUsageWindow.ToBusinessDate(x.CreatedAt),
                x.StatusCode))
            .Where(x => x.Date >= start && x.Date <= today)
            .ToList();

        return Enumerable.Range(0, boundedDays)
            .Select(offset =>
            {
                var date = start.AddDays(offset);
                var count = ApiUsageWindow.CountForDay(eligibleRows, date);
                return new ApiUsageSeriesPointDto(
                    date.ToString("yyyy-MM-dd"),
                    count.Calls,
                    count.Succeeded,
                    count.Failed);
            })
            .ToList();
    }
}
