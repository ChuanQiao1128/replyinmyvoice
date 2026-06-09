using ReplyInMyVoice.Application.Common;

namespace ReplyInMyVoice.Application.UseCases.ApiKey;

internal static class ApiUsageWindow
{
    private const int MinUsageWindowDays = 1;
    public const int MaxUsageWindowDays = 90;

    private static readonly TimeZoneInfo BusinessTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("Pacific/Auckland");

    public static int ClampUsageWindowDays(int days) =>
        Math.Clamp(days, MinUsageWindowDays, MaxUsageWindowDays);

    public static DateOnly ToBusinessDate(DateTimeOffset value)
    {
        var local = TimeZoneInfo.ConvertTime(value, BusinessTimeZone);
        return DateOnly.FromDateTime(local.Date);
    }

    public static DateTimeOffset ToBusinessDateStartUtc(DateOnly date)
    {
        var localDateTime = date.ToDateTime(TimeOnly.MinValue);
        var offset = BusinessTimeZone.GetUtcOffset(localDateTime);
        return new DateTimeOffset(localDateTime, offset).ToUniversalTime();
    }

    public static ApiUsageCountDto CountForDay(
        IReadOnlyList<LocalUsageRow> rows,
        DateOnly date) =>
        CountForRange(rows, date, date);

    public static ApiUsageCountDto CountForRange(
        IReadOnlyList<LocalUsageRow> rows,
        DateOnly start,
        DateOnly end)
    {
        var matched = rows
            .Where(x => x.Date >= start && x.Date <= end)
            .ToList();
        var succeeded = matched.Count(x => IsSucceeded(x.StatusCode));
        return new ApiUsageCountDto(
            matched.Count,
            succeeded,
            matched.Count - succeeded);
    }

    public static bool IsSucceeded(int statusCode) =>
        statusCode is 200 or 202;

    public sealed record LocalUsageRow(DateOnly Date, int StatusCode);
}
