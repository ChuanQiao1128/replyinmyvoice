using ReplyInMyVoice.Domain.Entities;

namespace ReplyInMyVoice.Application.Abstractions;

public interface IDeadLetterMessageRepository
{
    Task AddAsync(
        DeadLetterMessage message,
        CancellationToken ct = default);

    Task<DeadLetterMessagePage> GetPagedAsync(
        int page,
        int pageSize,
        string? sourceType,
        CancellationToken ct = default);

    Task<DeadLetterMessage?> GetByIdAsync(
        Guid id,
        bool track = false,
        CancellationToken ct = default);

    Task<bool> UpdateRequeuedAtAsync(
        Guid id,
        DateTimeOffset requeuedAt,
        CancellationToken ct = default);
}

public sealed record DeadLetterMessagePage(
    int Page,
    int PageSize,
    int TotalCount,
    IReadOnlyList<DeadLetterMessage> Items);
