using ReplyInMyVoice.Application.Common;

namespace ReplyInMyVoice.Application.Abstractions;

public interface IAdminStatsRepository
{
    Task<AdminStatsDto> GetStatsAsync(
        DateTimeOffset now,
        TaxTurnoverSettings taxTurnoverSettings,
        int refundReviewCountThreshold,
        long refundReviewAmountThreshold,
        CancellationToken ct = default);
}
