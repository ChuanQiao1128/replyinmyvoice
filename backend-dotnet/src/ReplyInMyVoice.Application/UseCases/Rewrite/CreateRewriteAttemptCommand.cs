using ReplyInMyVoice.Domain.Contracts;

namespace ReplyInMyVoice.Application.UseCases.Rewrite;

public sealed record CreateRewriteAttemptCommand(
    Guid UserId,
    string IdempotencyKey,
    RewriteRequest Request,
    string PeriodKey,
    int QuotaLimit,
    DateTimeOffset Now,
    Guid? ApiKeyId = null,
    string? CorrelationId = null);
