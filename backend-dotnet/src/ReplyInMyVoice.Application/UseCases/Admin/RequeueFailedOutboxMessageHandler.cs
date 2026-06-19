using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Domain.Enums;

namespace ReplyInMyVoice.Application.UseCases.Admin;

public sealed class RequeueFailedOutboxMessageHandler(
    IOutboxMessageRepository outboxMessages,
    IDeadLetterRepository deadLetters,
    IUnitOfWork unitOfWork)
{
    public async Task<AdminDeadLetterRequeueResultDto> HandleAsync(
        RequeueFailedOutboxMessageCommand command,
        CancellationToken ct = default)
    {
        var message = await outboxMessages.GetByIdAsync(command.MessageId, ct);
        if (message is null)
        {
            return AdminDeadLetterRequeueResultDto.NotFound();
        }

        if (message.Status == OutboxMessageStatus.Failed)
        {
            message.Status = OutboxMessageStatus.Pending;
            message.AttemptCount = 0;
            message.LastError = null;
            message.NextAttemptAt = command.Now;
            message.LockedBy = null;
            message.LockedUntil = null;
        }
        else if (!IsAlreadyRequeued(message.Status, message.AttemptCount, message.LastError, message.LockedBy, message.LockedUntil))
        {
            return AdminDeadLetterRequeueResultDto.NotFound();
        }

        await deadLetters.RequeueAsync(
            command.MessageId.ToString("D"),
            DeadLetterEntityType.Outbox,
            ct);
        await unitOfWork.SaveChangesAsync(ct);

        return AdminDeadLetterRequeueResultDto.Success(
            DeadLetterEntityType.Outbox,
            command.MessageId.ToString("D"),
            message.Status.ToString(),
            message.AttemptCount,
            message.NextAttemptAt);
    }

    private static bool IsAlreadyRequeued(
        OutboxMessageStatus status,
        int attemptCount,
        string? lastError,
        string? lockedBy,
        DateTimeOffset? lockedUntil) =>
        status == OutboxMessageStatus.Pending &&
        attemptCount == 0 &&
        lastError is null &&
        lockedBy is null &&
        lockedUntil is null;
}

public sealed record RequeueFailedOutboxMessageCommand(
    Guid MessageId,
    string LockedBy,
    DateTimeOffset Now);

public sealed record AdminDeadLetterRequeueResultDto(
    AdminDeadLetterRequeueResultKind Kind,
    DeadLetterEntityType? EntityType,
    string? EntityId,
    string? Status,
    int? AttemptCount,
    DateTimeOffset? NextAttemptAt)
{
    public static AdminDeadLetterRequeueResultDto Success(
        DeadLetterEntityType entityType,
        string entityId,
        string status,
        int attemptCount,
        DateTimeOffset? nextAttemptAt) =>
        new(
            AdminDeadLetterRequeueResultKind.Success,
            entityType,
            entityId,
            status,
            attemptCount,
            nextAttemptAt);

    public static AdminDeadLetterRequeueResultDto NotFound() =>
        new(
            AdminDeadLetterRequeueResultKind.NotFound,
            null,
            null,
            null,
            null,
            null);
}

public enum AdminDeadLetterRequeueResultKind
{
    Success = 0,
    NotFound = 1,
}
