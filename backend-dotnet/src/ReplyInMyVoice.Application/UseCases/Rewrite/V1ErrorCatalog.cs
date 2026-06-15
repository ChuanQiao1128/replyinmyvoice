namespace ReplyInMyVoice.Application.UseCases.Rewrite;

public static class V1ErrorCatalog
{
    private const int StatusBadRequest = 400;
    private const int StatusUnauthorized = 401;
    private const int StatusPaymentRequired = 402;
    private const int StatusNotFound = 404;
    private const int StatusConflict = 409;
    private const int StatusTooManyRequests = 429;
    private const int StatusInternalServerError = 500;
    private const int StatusServiceUnavailable = 503;

    public const string InvalidRequestCode = "invalid_request";
    public const string InputTooLongCode = "input_too_long";
    public const string InvalidKeyCode = "invalid_key";
    public const string RateLimitUnavailableCode = "rate_limit_unavailable";
    public const string RateLimitedCode = "rate_limited";
    public const string ApiRequiresPaidPlanCode = "api_requires_paid_plan";
    public const string QuotaExhaustedCode = "quota_exhausted";
    public const string IdempotencyConflictCode = "idempotency_conflict";
    public const string RewriteFailedCode = "rewrite_failed";
    public const string NotFoundCode = "not_found";
    public const string EngineUnavailableCode = "engine_unavailable";

    public const string InvalidJsonMessage = "Request body must be valid JSON.";
    public const string DraftRequiredMessage = "A draft of at least 10 characters is required.";
    public const string InputTooLongMessage = "Draft must be 300 words or fewer and no more than 2400 characters.";
    public const string IdempotencyKeyTooLongMessage = "Idempotency-Key must be 120 characters or fewer.";
    public const string InvalidKeyMessage = "A valid API key is required.";
    public const string RateLimitUnavailableMessage = "Request limit could not be checked. Please retry later.";
    public const string RateLimitedMessage = "Request limit reached. Please retry later.";
    public const string ApiRequiresPaidPlanMessage = "Public API access requires an active paid plan or usable purchased rewrite credit.";
    public const string QuotaExhaustedMessage = "No rewrite quota remains for the current period.";
    public const string IdempotencyConflictMessage = "The idempotency key was reused with a different draft.";
    public const string RewriteFailedMessage = "The rewrite request could not be processed.";
    public const string RewriteNotFoundMessage = "Rewrite result was not found.";
    public const string RewriteExpiredMessage = "The rewrite did not finish in time. Please submit a new request.";
    public const string RewriteCouldNotBeCompletedMessage = "The rewrite could not be completed. Please try again.";

    public static readonly V1Error InvalidJson = new(InvalidRequestCode, InvalidJsonMessage, StatusBadRequest);
    public static readonly V1Error DraftRequired = new(InvalidRequestCode, DraftRequiredMessage, StatusBadRequest);
    public static readonly V1Error InputTooLong = new(InputTooLongCode, InputTooLongMessage, StatusBadRequest);
    public static readonly V1Error IdempotencyKeyTooLong = new(InvalidRequestCode, IdempotencyKeyTooLongMessage, StatusBadRequest);
    public static readonly V1Error InvalidKey = new(InvalidKeyCode, InvalidKeyMessage, StatusUnauthorized);
    public static readonly V1Error RateLimitUnavailable = new(RateLimitUnavailableCode, RateLimitUnavailableMessage, StatusServiceUnavailable);
    public static readonly V1Error RateLimited = new(RateLimitedCode, RateLimitedMessage, StatusTooManyRequests);
    public static readonly V1Error ApiRequiresPaidPlan = new(ApiRequiresPaidPlanCode, ApiRequiresPaidPlanMessage, StatusPaymentRequired);
    public static readonly V1Error QuotaExhausted = new(QuotaExhaustedCode, QuotaExhaustedMessage, StatusPaymentRequired);
    public static readonly V1Error IdempotencyConflict = new(IdempotencyConflictCode, IdempotencyConflictMessage, StatusConflict);
    public static readonly V1Error RewriteFailed = new(RewriteFailedCode, RewriteFailedMessage, StatusInternalServerError);
    public static readonly V1Error NotFound = new(NotFoundCode, RewriteNotFoundMessage, StatusNotFound);

    public sealed record V1Error(string Code, string Message, int StatusCode);
}
