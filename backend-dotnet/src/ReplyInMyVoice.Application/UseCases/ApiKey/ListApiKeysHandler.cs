using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.Common;

namespace ReplyInMyVoice.Application.UseCases.ApiKey;

public sealed class ListApiKeysHandler(
    IApiKeyRepository apiKeys,
    IApiKeyUsageRepository usage)
{
    public async Task<IReadOnlyList<ApiKeySummaryDto>> HandleAsync(
        ListApiKeysQuery query,
        CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var usageStart = now.AddDays(-30);
        var keys = await apiKeys.ListByUserIdAsync(query.UserId, ct);
        var keyIds = keys.Select(x => x.Id).ToArray();
        var usageByKeyId = await usage.CountByApiKeyAsync(keyIds, usageStart, now, ct);

        return keys
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new ApiKeySummaryDto(
                x.Id,
                x.Name,
                ApiKeyCredential.MaskKey(x.Last4, x.IsTest),
                x.IsTest,
                x.LastUsedAt,
                x.CreatedAt,
                x.RevokedAt,
                x.WebhookUrl,
                usageByKeyId.GetValueOrDefault(x.Id, new ApiUsageCountDto(0, 0, 0))))
            .ToList();
    }
}
