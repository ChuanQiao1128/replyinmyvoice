using System.Data;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;

namespace ReplyInMyVoice.Application.UseCases.StripeEvent;

public sealed class IngestStripeWebhookHandler(
    IStripeEventRepository stripeEvents,
    IUnitOfWork unitOfWork)
{
    public async Task<StripeWebhookIngestResult> HandleAsync(
        IngestStripeWebhookCommand command,
        CancellationToken ct = default)
    {
        try
        {
            return await unitOfWork.ExecuteInTransactionAsync(
                async transactionCt =>
                {
                    var stripeEvent = await stripeEvents.GetByEventIdAsync(command.EventId, transactionCt);
                    if (stripeEvent is null)
                    {
                        await stripeEvents.AddAsync(new Domain.Entities.StripeEvent
                        {
                            EventId = command.EventId,
                            Type = command.Type,
                            Status = StripeEventStatus.Pending,
                            AttemptCount = 0,
                            CreatedAt = command.Now,
                            LockedUntil = null,
                            PayloadJson = command.RawBody,
                        }, transactionCt);
                        await unitOfWork.SaveChangesAsync(transactionCt);
                        return StripeWebhookIngestResult.Accepted;
                    }

                    if (stripeEvent.Status == StripeEventStatus.Processed)
                    {
                        return StripeWebhookIngestResult.AlreadyProcessed;
                    }

                    if (stripeEvent.Status == StripeEventStatus.Processing &&
                        stripeEvent.LockedUntil > command.Now)
                    {
                        return StripeWebhookIngestResult.AlreadyPending;
                    }

                    stripeEvent.Type = command.Type;
                    stripeEvent.Status = StripeEventStatus.Pending;
                    stripeEvent.LockedUntil = null;
                    stripeEvent.PayloadJson = command.RawBody;
                    stripeEvent.ProcessedAt = null;
                    await unitOfWork.SaveChangesAsync(transactionCt);
                    return StripeWebhookIngestResult.AlreadyPending;
                },
                IsolationLevel.Serializable,
                ct);
        }
        catch (Exception ex) when (stripeEvents.IsDuplicateEventWriteFailure(ex))
        {
            return StripeWebhookIngestResult.AlreadyPending;
        }
    }
}
