using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.Common;

namespace ReplyInMyVoice.Application.UseCases.ApiKey;

public sealed class RehashPendingApiKeysHandler(
    IApiKeyRepository apiKeys,
    IUnitOfWork unitOfWork)
{
    private const int MaxBatchSize = 500;

    public async Task<RehashPendingApiKeysResult> HandleAsync(
        RehashPendingApiKeysCommand command,
        CancellationToken ct = default)
    {
        var batchSize = Math.Clamp(command.BatchSize, 1, MaxBatchSize);
        var currentPepperVersion = ApiKeyCredential.CurrentPepperVersion;
        var pendingKeys = await apiKeys.ListRehashPendingAsync(batchSize, ct);
        var cleared = 0;
        var now = DateTimeOffset.UtcNow;

        foreach (var apiKey in pendingKeys)
        {
            if (apiKey.PepperVersion != currentPepperVersion)
            {
                continue;
            }

            apiKey.RehashPending = false;
            apiKey.UpdatedAt = now;
            cleared++;
        }

        if (cleared > 0)
        {
            await unitOfWork.SaveChangesAsync(ct);
        }

        return new RehashPendingApiKeysResult(pendingKeys.Count, cleared);
    }
}
