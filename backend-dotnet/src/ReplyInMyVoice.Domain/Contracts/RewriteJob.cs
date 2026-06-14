namespace ReplyInMyVoice.Domain.Contracts;

public sealed record RewriteJob(
    Guid AttemptId,
    string? CorrelationId = null,
    string? Traceparent = null);
