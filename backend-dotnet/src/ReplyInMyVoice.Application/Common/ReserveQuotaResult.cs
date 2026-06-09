using ReplyInMyVoice.Domain.Enums;

namespace ReplyInMyVoice.Application.Common;

public enum ReserveQuotaResultKind
{
    Created,
    Existing,
    QuotaExceeded,
    Conflict,
}

public sealed record ReserveQuotaResult(
    ReserveQuotaResultKind Kind,
    Guid AttemptId,
    RewriteAttemptStatus Status,
    string? ResultJson = null,
    string? ErrorCode = null)
{
    public static ReserveQuotaResult QuotaExceeded() =>
        new(
            ReserveQuotaResultKind.QuotaExceeded,
            Guid.Empty,
            RewriteAttemptStatus.Failed,
            ErrorCode: "quota_exhausted");

    public static ReserveQuotaResult Conflict(Guid attemptId, RewriteAttemptStatus status) =>
        new(
            ReserveQuotaResultKind.Conflict,
            attemptId,
            status,
            ErrorCode: "idempotency_key_reused_with_different_request");
}
