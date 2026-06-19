using ReplyInMyVoice.Domain.Enums;

namespace ReplyInMyVoice.Application.Abstractions;

public interface IDeadLetterRepository
{
    Task RecordFailureAsync(
        DeadLetterEntityType entityType,
        string entityId,
        string reason,
        CancellationToken ct = default);

    Task<DeadLetterFailurePage> GetFailuresAsync(
        DeadLetterEntityType? entityType,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<DeadLetterRequeueResult> RequeueAsync(
        string entityId,
        DeadLetterEntityType entityType,
        CancellationToken ct = default);
}

public sealed record DeadLetterFailurePage(
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages,
    IReadOnlyList<DeadLetterFailureDto> Items);

public sealed record DeadLetterFailureDto(
    Guid Id,
    DeadLetterEntityType EntityType,
    string EntityId,
    string FailureReason,
    int FailureCount,
    DateTimeOffset FirstFailedAt,
    DateTimeOffset LastFailedAt,
    DateTimeOffset CreatedAt);

public sealed record DeadLetterRequeueResult(
    DeadLetterRequeueResultKind Kind,
    Guid? Id);

public enum DeadLetterRequeueResultKind
{
    Success = 0,
    NotFound = 1,
}
