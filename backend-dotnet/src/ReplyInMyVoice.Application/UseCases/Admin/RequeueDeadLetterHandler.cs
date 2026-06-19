using System.Data;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.Common;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;

namespace ReplyInMyVoice.Application.UseCases.Admin;

public sealed class RequeueDeadLetterHandler(
    IDeadLetterMessageRepository deadLetters,
    IOutboxMessageRepository outboxMessages,
    IStripeEventRepository stripeEvents,
    IAdminUserRepository adminUsers,
    IUnitOfWork unitOfWork)
{
    public async Task<AdminDeadLetterRequeueResultDto> HandleAsync(
        RequeueDeadLetterCommand command,
        CancellationToken ct = default)
    {
        try
        {
            return await unitOfWork.ExecuteInTransactionAsync(
                async transactionCt =>
                {
                    var deadLetter = await deadLetters.GetByIdAsync(command.DeadLetterId, track: true, transactionCt);
                    if (deadLetter is null)
                    {
                        return AdminDeadLetterRequeueResultDto.NotFound("Dead letter was not found.");
                    }

                    if (deadLetter.RequeuedAt is not null)
                    {
                        return AdminDeadLetterRequeueResultDto.AlreadyRequeued("Dead letter has already been requeued.");
                    }

                    var metadata = DeadLetterMessageSupport.ReadMetadata(deadLetter);
                    var result = deadLetter.SourceType switch
                    {
                        DeadLetterSourceTypes.OutboxMessage => await ResetOutboxAsync(deadLetter, command.Now, transactionCt),
                        DeadLetterSourceTypes.StripeEvent => await ResetStripeEventAsync(deadLetter, command.Now, transactionCt),
                        _ => ResetResult.InvalidSource("Dead letter source type is not supported."),
                    };

                    if (result.Kind != ResetResultKind.Success)
                    {
                        return result.Kind switch
                        {
                            ResetResultKind.NotFound => AdminDeadLetterRequeueResultDto.OriginalNotFound(result.Detail),
                            ResetResultKind.InvalidState => AdminDeadLetterRequeueResultDto.InvalidOriginalState(result.Detail),
                            _ => AdminDeadLetterRequeueResultDto.InvalidSource(result.Detail),
                        };
                    }

                    var marked = await deadLetters.UpdateRequeuedAtAsync(deadLetter.Id, command.Now, transactionCt);
                    if (!marked)
                    {
                        return AdminDeadLetterRequeueResultDto.AlreadyRequeued("Dead letter has already been requeued.");
                    }

                    await adminUsers.AddAuditLogAsync(new AdminAuditLog
                    {
                        AdminExternalAuthUserId = command.AdminExternalAuthUserId.Trim(),
                        AdminEmail = command.AdminEmail?.Trim() ?? string.Empty,
                        Action = "requeue_dead_letter",
                        TargetUserId = null,
                        DetailsJson = AdminUseCaseSupport.SerializeAuditDetails(
                            new AdminDeadLetterAuditDetailsDto(
                                deadLetter.SourceType,
                                deadLetter.SourceId,
                                metadata.AttemptCount ?? result.AttemptCount,
                                metadata.LastError ?? result.LastError)),
                        CreatedAt = command.Now,
                    }, transactionCt);

                    await unitOfWork.SaveChangesAsync(transactionCt);
                    return AdminDeadLetterRequeueResultDto.Success(DeadLetterMessageSupport.ToDetail(deadLetter));
                },
                IsolationLevel.Serializable,
                ct);
        }
        catch (Exception ex) when (string.Equals(
            ex.GetType().Name,
            "DbUpdateConcurrencyException",
            StringComparison.Ordinal))
        {
            return AdminDeadLetterRequeueResultDto.AlreadyRequeued("Dead letter has already been requeued.");
        }
    }

    private async Task<ResetResult> ResetOutboxAsync(
        DeadLetterMessage deadLetter,
        DateTimeOffset now,
        CancellationToken ct)
    {
        if (!Guid.TryParse(deadLetter.SourceId, out var outboxId))
        {
            return ResetResult.InvalidSource("Dead letter source id is not a valid outbox message id.");
        }

        var message = await outboxMessages.GetByIdAsync(outboxId, ct);
        if (message is null)
        {
            return ResetResult.NotFound("Original outbox message was not found.");
        }

        if (message.Status == OutboxMessageStatus.Sent)
        {
            return ResetResult.InvalidState("Original outbox message has already been sent.");
        }

        if (message.Status != OutboxMessageStatus.Failed)
        {
            return ResetResult.InvalidState("Original outbox message is not failed.");
        }

        var attemptCount = message.AttemptCount;
        var lastError = message.LastError;
        message.Status = OutboxMessageStatus.Pending;
        message.AttemptCount = 0;
        message.NextAttemptAt = now;
        message.LockedBy = null;
        message.LockedUntil = null;
        message.LastError = null;
        message.LastAttemptAt = null;
        message.SentAt = null;

        return ResetResult.Success(attemptCount, lastError);
    }

    private async Task<ResetResult> ResetStripeEventAsync(
        DeadLetterMessage deadLetter,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var stripeEvent = await stripeEvents.GetByEventIdAsync(deadLetter.SourceId, ct);
        if (stripeEvent is null)
        {
            return ResetResult.NotFound("Original Stripe event was not found.");
        }

        if (stripeEvent.Status == StripeEventStatus.Processed)
        {
            return ResetResult.InvalidState("Original Stripe event has already been processed.");
        }

        if (stripeEvent.Status != StripeEventStatus.Failed)
        {
            return ResetResult.InvalidState("Original Stripe event is not failed.");
        }

        var attemptCount = stripeEvent.AttemptCount;
        var lastError = stripeEvent.LastError;
        stripeEvent.Status = StripeEventStatus.Pending;
        stripeEvent.AttemptCount = 0;
        stripeEvent.LockedUntil = null;
        stripeEvent.LastError = null;
        stripeEvent.LastAttemptAt = null;
        stripeEvent.ProcessedAt = null;

        return ResetResult.Success(attemptCount, lastError);
    }

    private sealed record ResetResult(
        ResetResultKind Kind,
        int? AttemptCount = null,
        string? LastError = null,
        string Detail = "")
    {
        public static ResetResult Success(int? attemptCount, string? lastError) =>
            new(ResetResultKind.Success, attemptCount, lastError);

        public static ResetResult NotFound(string detail) =>
            new(ResetResultKind.NotFound, Detail: detail);

        public static ResetResult InvalidSource(string detail) =>
            new(ResetResultKind.InvalidSource, Detail: detail);

        public static ResetResult InvalidState(string detail) =>
            new(ResetResultKind.InvalidState, Detail: detail);
    }

    private enum ResetResultKind
    {
        Success,
        NotFound,
        InvalidSource,
        InvalidState,
    }
}
