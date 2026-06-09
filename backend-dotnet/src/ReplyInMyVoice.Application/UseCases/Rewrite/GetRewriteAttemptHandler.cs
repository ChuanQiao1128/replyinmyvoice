using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.Common;

namespace ReplyInMyVoice.Application.UseCases.Rewrite;

public sealed class GetRewriteAttemptHandler(IRewriteAttemptRepository attempts)
{
    public async Task<ApplicationResult<RewriteAttemptDto>> HandleAsync(
        GetRewriteAttemptQuery query,
        CancellationToken ct = default)
    {
        var attempt = await attempts.GetByIdForUserAsync(query.AttemptId, query.UserId, ct);
        return attempt is null
            ? ApplicationResult<RewriteAttemptDto>.NotFound()
            : ApplicationResult<RewriteAttemptDto>.Success(RewriteAttemptDto.FromAttempt(attempt));
    }
}
