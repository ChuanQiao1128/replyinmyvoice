using System.Data;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;

namespace ReplyInMyVoice.Application.UseCases.Quota;

public sealed class MarkQuotaProcessingHandler(
    IRewriteAttemptRepository attempts,
    IUnitOfWork unitOfWork)
{
    public async Task<bool> HandleAsync(
        MarkQuotaProcessingCommand command,
        CancellationToken ct = default) =>
        await unitOfWork.ExecuteInTransactionAsync(
            async transactionCt =>
            {
                var attempt = await RequireAttemptAsync(command.AttemptId, transactionCt);

                if (attempt.Status is not RewriteAttemptStatus.Pending)
                {
                    return false;
                }

                attempt.Status = RewriteAttemptStatus.Processing;
                attempt.RowVersion = Guid.NewGuid();
                await unitOfWork.SaveChangesAsync(transactionCt);
                return true;
            },
            IsolationLevel.Serializable,
            ct);

    private async Task<RewriteAttempt> RequireAttemptAsync(Guid attemptId, CancellationToken ct) =>
        await attempts.GetByIdAsync(attemptId, ct) ??
        throw new InvalidOperationException($"Rewrite attempt '{attemptId}' was not found.");
}
