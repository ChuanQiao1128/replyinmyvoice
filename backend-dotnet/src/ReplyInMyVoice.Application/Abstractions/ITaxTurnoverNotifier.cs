using ReplyInMyVoice.Application.Common;

namespace ReplyInMyVoice.Application.Abstractions;

public interface ITaxTurnoverNotifier
{
    Task<TaxTurnoverNotificationResultDto> TrySendWarningNotificationAsync(
        TaxTurnoverNotificationRequest request,
        CancellationToken ct = default);
}

public sealed record TaxTurnoverNotificationRequest(
    DateTimeOffset WindowEnd,
    long GrossAmountTotal,
    long RegistrationThresholdAmountTotal,
    decimal WarningFraction);
