using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.Common;

namespace ReplyInMyVoice.Application.UseCases.ApiKey;

public sealed class GetApiUsageRecentHandler(IApiKeyUsageRepository usage)
{
    public async Task<IReadOnlyList<ApiUsageRecentItemDto>> HandleAsync(
        GetApiUsageRecentQuery query,
        CancellationToken ct = default)
    {
        var boundedLimit = query.Limit <= 0 ? 50 : Math.Min(query.Limit, 200);
        var windowStart = ApiUsageWindow.ToBusinessDateStartUtc(
            ApiUsageWindow.ToBusinessDate(query.Now).AddDays(-(ApiUsageWindow.MaxUsageWindowDays - 1)));
        return await usage.ListRecentAsync(query.UserId, windowStart, boundedLimit, ct);
    }
}
