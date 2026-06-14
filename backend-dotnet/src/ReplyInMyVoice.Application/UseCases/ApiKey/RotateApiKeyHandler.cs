using System.Data;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.Common;
using ApiKeyEntity = ReplyInMyVoice.Domain.Entities.ApiKey;

namespace ReplyInMyVoice.Application.UseCases.ApiKey;

public sealed class RotateApiKeyHandler(
    IApiKeyRepository apiKeys,
    IUnitOfWork unitOfWork)
{
    public async Task<ApiKeyRotationResultDto?> HandleAsync(
        RotateApiKeyCommand command,
        CancellationToken ct = default) =>
        await unitOfWork.ExecuteInTransactionAsync<ApiKeyRotationResultDto?>(
            async transactionCt =>
            {
                var apiKey = await apiKeys.GetByIdForUserAsync(
                    command.UserId,
                    command.KeyId,
                    transactionCt);
                if (apiKey is null || apiKey.RevokedAt is not null)
                {
                    return null;
                }

                var plaintext = ApiKeyCredential.GeneratePlaintext(apiKey.IsTest);
                var now = DateTimeOffset.UtcNow;
                apiKey.RevokedAt = now;
                apiKey.UpdatedAt = now;

                var replacement = new ApiKeyEntity
                {
                    UserId = command.UserId,
                    Name = apiKey.Name,
                    KeyHash = ApiKeyCredential.ComputeHash(plaintext),
                    Last4 = plaintext[^4..],
                    IsTest = apiKey.IsTest,
                    PlanTier = apiKey.PlanTier,
                    Scope = apiKey.Scope,
                    RateLimitPerMinute = apiKey.RateLimitPerMinute,
                    MonthlyQuota = apiKey.MonthlyQuota,
                    WebhookUrl = apiKey.WebhookUrl,
                    WebhookSecret = apiKey.WebhookSecret,
                    CreatedAt = now,
                    UpdatedAt = now,
                };

                await apiKeys.AddAsync(replacement, transactionCt);
                await unitOfWork.SaveChangesAsync(transactionCt);
                return new ApiKeyRotationResultDto(
                    replacement.Id,
                    replacement.Name,
                    plaintext,
                    replacement.CreatedAt,
                    replacement.IsTest);
            },
            IsolationLevel.Serializable,
            ct);
}
