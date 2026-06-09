using ReplyInMyVoice.Domain.Entities;

namespace ReplyInMyVoice.Application.Common;

public sealed record RewriteAttemptDto(
    Guid AttemptId,
    string Status,
    string IdempotencyKey,
    string RequestJson,
    string? ResultJson,
    string? ErrorCode,
    string? ErrorMessage,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt)
{
    public static RewriteAttemptDto FromAttempt(RewriteAttempt attempt) =>
        new(
            attempt.Id,
            attempt.Status.ToString(),
            attempt.IdempotencyKey,
            attempt.RequestJson,
            attempt.ResultJson,
            attempt.ErrorCode,
            attempt.ErrorMessage,
            attempt.CreatedAt,
            attempt.CompletedAt);
}
