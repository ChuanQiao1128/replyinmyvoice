using ReplyInMyVoice.Application.Common;

namespace ReplyInMyVoice.Application.Abstractions;

public interface IStripeReconciliationAlerter
{
    Task AlertAsync(
        StripeReconciliationReportDto report,
        CancellationToken ct = default);
}
