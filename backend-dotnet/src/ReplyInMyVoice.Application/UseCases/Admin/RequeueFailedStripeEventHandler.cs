using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Domain.Enums;

namespace ReplyInMyVoice.Application.UseCases.Admin;

public sealed class RequeueFailedStripeEventHandler(
    IStripeEventRepository stripeEvents,
    IDeadLetterRepository deadLetters,
    IUnitOfWork unitOfWork)
{
    public async Task<AdminDeadLetterRequeueResultDto> HandleAsync(
        RequeueFailedStripeEventCommand command,
        CancellationToken ct = default)
    {
        var stripeEvent = await stripeEvents.GetByEventIdAsync(command.EventId, ct);
        if (stripeEvent is null)
        {
            return AdminDeadLetterRequeueResultDto.NotFound();
        }

        if (stripeEvent.Status == StripeEventStatus.Failed)
        {
            stripeEvent.Status = StripeEventStatus.Pending;
            stripeEvent.AttemptCount = 0;
            stripeEvent.LastError = null;
            stripeEvent.LockedUntil = null;
            stripeEvent.ProcessedAt = null;
        }
        else if (!IsAlreadyRequeued(stripeEvent.Status, stripeEvent.AttemptCount, stripeEvent.LastError, stripeEvent.LockedUntil))
        {
            return AdminDeadLetterRequeueResultDto.NotFound();
        }

        await deadLetters.RequeueAsync(
            command.EventId,
            DeadLetterEntityType.Stripe,
            ct);
        await unitOfWork.SaveChangesAsync(ct);

        return AdminDeadLetterRequeueResultDto.Success(
            DeadLetterEntityType.Stripe,
            command.EventId,
            stripeEvent.Status.ToString(),
            stripeEvent.AttemptCount,
            null);
    }

    private static bool IsAlreadyRequeued(
        StripeEventStatus status,
        int attemptCount,
        string? lastError,
        DateTimeOffset? lockedUntil) =>
        status == StripeEventStatus.Pending &&
        attemptCount == 0 &&
        lastError is null &&
        lockedUntil is null;
}

public sealed record RequeueFailedStripeEventCommand(
    string EventId,
    DateTimeOffset Now);
