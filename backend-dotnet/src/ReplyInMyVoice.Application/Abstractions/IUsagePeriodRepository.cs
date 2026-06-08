using ReplyInMyVoice.Domain.Entities;

namespace ReplyInMyVoice.Application.Abstractions;

public interface IUsagePeriodRepository
{
    Task AddAsync(UsagePeriod usagePeriod, CancellationToken ct = default);

    Task<UsagePeriod?> GetByUserIdAndPeriodKeyAsync(
        Guid userId,
        string periodKey,
        CancellationToken ct = default);
}
