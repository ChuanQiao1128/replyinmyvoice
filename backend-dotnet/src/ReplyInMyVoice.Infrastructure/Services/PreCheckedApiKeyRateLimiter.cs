namespace ReplyInMyVoice.Infrastructure.Services;

public sealed class PreCheckedApiKeyRateLimiter(
    IApiKeyRateLimiter inner,
    InProcessRateLimitGate gate) : IApiKeyRateLimiter
{
    public async Task<ApiKeyRateLimitResult> CheckAndIncrementAsync(
        Guid apiKeyId,
        int rateLimitPerMinute,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (rateLimitPerMinute <= 0)
        {
            return await inner.CheckAndIncrementAsync(
                apiKeyId,
                rateLimitPerMinute,
                now,
                cancellationToken);
        }

        if (gate.ShouldShed(apiKeyId, rateLimitPerMinute, now, out var resetAt))
        {
            return ApiKeyRateLimitResult.Limited(rateLimitPerMinute, rateLimitPerMinute, resetAt);
        }

        return await inner.CheckAndIncrementAsync(
            apiKeyId,
            rateLimitPerMinute,
            now,
            cancellationToken);
    }
}
