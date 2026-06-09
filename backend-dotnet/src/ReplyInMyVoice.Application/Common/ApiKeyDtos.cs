namespace ReplyInMyVoice.Application.Common;

public sealed record GeneratedApiKeyDto(
    Guid Id,
    string Plaintext,
    DateTimeOffset CreatedAt);

public sealed record ApiKeySummaryDto(
    Guid Id,
    string Name,
    string MaskedKey,
    bool IsTest,
    DateTimeOffset? LastUsedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? RevokedAt,
    string? WebhookUrl,
    ApiUsageCountDto Last30dUsage);

public sealed record ApiKeyRotationResultDto(
    Guid Id,
    string Name,
    string Plaintext,
    DateTimeOffset CreatedAt,
    bool IsTest);

public sealed record ApiKeyWebhookResultDto(
    Guid Id,
    string WebhookUrl,
    string WebhookSecret);

public sealed record ApiUsageSummaryDto(
    ApiUsageCountDto Today,
    ApiUsageCountDto Yesterday,
    ApiUsageCountDto MonthToDate,
    int Last30dCalls,
    int Quota,
    int Used,
    int Remaining,
    DateTimeOffset? PeriodEnd);

public sealed record ApiUsageCountDto(int Calls, int Succeeded, int Failed);

public sealed record ApiUsageSeriesPointDto(
    string Date,
    int Calls,
    int Succeeded,
    int Failed);

public sealed record ApiUsageRecentItemDto(
    DateTimeOffset CreatedAt,
    string Endpoint,
    int StatusCode,
    int? LatencyMs,
    string? KeyLast4);

public sealed record ApiUsageRowDto(DateTimeOffset CreatedAt, int StatusCode);
