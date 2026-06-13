using ReplyInMyVoice.Domain.Entities;

namespace ReplyInMyVoice.Application.Abstractions;

public interface IStripeReconciliationRunRepository
{
    Task AddAsync(
        StripeReconciliationRun run,
        CancellationToken ct = default);
}
