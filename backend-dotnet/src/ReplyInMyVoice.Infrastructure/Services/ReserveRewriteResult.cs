using ReplyInMyVoice.Domain.Enums;

namespace ReplyInMyVoice.Infrastructure.Services;

public enum ReserveRewriteResultKind
{
    Created,
    Existing,
    QuotaExceeded,
}

public sealed record ReserveRewriteResult(
    ReserveRewriteResultKind Kind,
    Guid AttemptId,
    RewriteAttemptStatus Status,
    string? ResultJson = null,
    string? ErrorCode = null)
{
    public static ReserveRewriteResult QuotaExceeded() =>
        new(ReserveRewriteResultKind.QuotaExceeded, Guid.Empty, RewriteAttemptStatus.Failed, ErrorCode: "quota_exhausted");
}
