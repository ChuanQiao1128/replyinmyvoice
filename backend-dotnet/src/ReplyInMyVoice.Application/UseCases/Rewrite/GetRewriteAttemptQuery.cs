namespace ReplyInMyVoice.Application.UseCases.Rewrite;

public sealed record GetRewriteAttemptQuery(Guid AttemptId, Guid UserId);
