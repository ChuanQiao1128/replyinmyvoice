using System.Data;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Domain.Enums;

namespace ReplyInMyVoice.Application.UseCases.StripeEvent;

public sealed class TryMarkStripeEventProcessedHandler(
    IStripeEventRepository stripeEvents,
    IUnitOfWork unitOfWork)
{
    public async Task<bool> HandleAsync(
        TryMarkStripeEventProcessedCommand command,
        CancellationToken ct = default)
    {
        try
        {
            return await unitOfWork.ExecuteInTransactionAsync(
                async transactionCt =>
                {
                    var existing = await stripeEvents.GetByEventIdAsync(command.EventId, transactionCt);
                    if (existing is not null)
                    {
                        return false;
                    }

                    await stripeEvents.AddAsync(new Domain.Entities.StripeEvent
                    {
                        EventId = command.EventId,
                        Type = command.Type,
                        Status = StripeEventStatus.Processed,
                        AttemptCount = 1,
                        CreatedAt = command.Now,
                        LastAttemptAt = command.Now,
                        ProcessedAt = command.Now,
                    }, transactionCt);
                    await unitOfWork.SaveChangesAsync(transactionCt);
                    return true;
                },
                IsolationLevel.Serializable,
                ct);
        }
        catch (Exception ex) when (stripeEvents.IsDuplicateEventWriteFailure(ex))
        {
            return false;
        }
    }
}
