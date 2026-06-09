using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.Common;

namespace ReplyInMyVoice.Application.UseCases.Admin;

public sealed class GetAdminStatsHandler(
    IAdminStatsRepository adminStats,
    ITaxTurnoverSettingsProvider taxTurnoverSettingsProvider,
    ITaxTurnoverNotifier? taxTurnoverNotifier = null)
{
    public const int RefundReviewCountThreshold = 3;
    public const long RefundReviewAmountThreshold = 2_500;

    public async Task<AdminStatsDto> HandleAsync(
        GetAdminStatsQuery query,
        CancellationToken ct = default)
    {
        var stats = await adminStats.GetStatsAsync(
            query.Now,
            taxTurnoverSettingsProvider.GetSettings(),
            RefundReviewCountThreshold,
            RefundReviewAmountThreshold,
            ct);

        if (stats.GstTurnover.Warning is null || taxTurnoverNotifier is null)
        {
            return stats;
        }

        var notification = await taxTurnoverNotifier.TrySendWarningNotificationAsync(
            new TaxTurnoverNotificationRequest(
                stats.GstTurnover.WindowEnd,
                stats.GstTurnover.GrossAmountTotal,
                stats.GstTurnover.RegistrationThresholdAmountTotal,
                stats.GstTurnover.WarningFraction),
            ct);

        return stats with
        {
            GstTurnover = stats.GstTurnover with
            {
                Notification = notification,
            },
        };
    }
}
