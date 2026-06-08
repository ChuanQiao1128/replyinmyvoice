using ReplyInMyVoice.Domain.Entities;

namespace ReplyInMyVoice.Application.Common;

public sealed record RewriteAttemptDto(
    Guid AttemptId,
    string Status,
    string? ResultJson,
    string? ErrorCode)
{
    public static RewriteAttemptDto FromAttempt(RewriteAttempt attempt) =>
        new(
            attempt.Id,
            attempt.Status.ToString(),
            attempt.ResultJson,
            attempt.ErrorCode);
}
