using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.Common;
using ApiKeyEntity = ReplyInMyVoice.Domain.Entities.ApiKey;

namespace ReplyInMyVoice.Application.UseCases.ApiKey;

public sealed class GenerateApiKeyHandler(
    IApiKeyRepository apiKeys,
    IUnitOfWork unitOfWork)
{
    public async Task<GeneratedApiKeyDto> HandleAsync(
        GenerateApiKeyCommand command,
        CancellationToken ct = default)
    {
        var plaintext = ApiKeyCredential.GeneratePlaintext(command.IsTest);
        var pepperVersion = ApiKeyPepperVersions.GetCurrentPepperVersion();
        var now = DateTimeOffset.UtcNow;
        var apiKey = new ApiKeyEntity
        {
            UserId = command.UserId,
            Name = command.Name,
            KeyHash = ApiKeyCredential.ComputeHashWithVersion(plaintext, pepperVersion),
            PepperVersion = pepperVersion,
            Last4 = plaintext[^4..],
            IsTest = command.IsTest,
            CreatedAt = now,
            UpdatedAt = now,
        };

        await apiKeys.AddAsync(apiKey, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return new GeneratedApiKeyDto(apiKey.Id, plaintext, apiKey.CreatedAt);
    }
}
