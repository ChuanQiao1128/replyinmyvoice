using FluentAssertions;
using ReplyInMyVoice.Application.Common;
using ReplyInMyVoice.Infrastructure.Services;

namespace ReplyInMyVoice.Tests.ApiKeyPepperRotation;

[Collection("ApiKeyPepper")]
public sealed class ApiKeyCredentialDualValidationTests
{
    [Fact]
    public void ComputeHashWithVersion_mirrors_ApiKeyHashing()
    {
        using var env = new PepperEnvironmentScope(new Dictionary<string, string?>
        {
            ["API_KEY_PEPPER"] = TestValue('a'),
            ["API_KEY_PEPPER_V2"] = TestValue('b'),
        });
        const string token = "rmv_live_credential_mirror_token";

        ApiKeyCredential.ComputeHashWithVersion(token, 1)
            .Should()
            .Be(ApiKeyHashing.ComputeHashWithVersion(token, 1));
        ApiKeyCredential.ComputeHashWithVersion(token, 2)
            .Should()
            .Be(ApiKeyHashing.ComputeHashWithVersion(token, 2));
    }

    private static string TestValue(char value) => new(value, 40);
}
