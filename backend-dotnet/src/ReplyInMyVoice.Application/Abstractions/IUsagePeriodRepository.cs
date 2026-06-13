using ReplyInMyVoice.Domain.Entities;

namespace ReplyInMyVoice.Application.Abstractions;

public interface IUsagePeriodRepository
{
    Task AddAsync(UsagePeriod usagePeriod, CancellationToken ct = default);

    Task<UsagePeriod?> GetByIdAsync(Guid usagePeriodId, CancellationToken ct = default);

    Task<UsagePeriod?> GetByUserIdAndPeriodKeyAsync(
        Guid userId,
        string periodKey,
        CancellationToken ct = default);

    Task<int> TryReserveSlotAsync(
        Guid usagePeriodId,
        int quotaLimit,
        DateTimeOffset now,
        CancellationToken ct = default);

    Task<int> RefreshQuotaLimitAsync(
        Guid usagePeriodId,
        int quotaLimit,
        DateTimeOffset now,
        CancellationToken ct = default);

    Task<int> FinalizeReservedSlotAsync(
        Guid usagePeriodId,
        DateTimeOffset now,
        CancellationToken ct = default);

    Task<int> ReleaseReservedSlotAsync(
        Guid usagePeriodId,
        DateTimeOffset now,
        CancellationToken ct = default);

    Task<IReadOnlyList<UsagePeriod>> ListByUserIdAsync(
        Guid userId,
        CancellationToken ct = default);
}
