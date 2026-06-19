using System.Reflection;
using FluentAssertions;
using ReplyInMyVoice.Infrastructure.Services;

namespace ReplyInMyVoice.Tests.ApiKeyPepperRotation;

[Collection("ApiKeyPepper")]
public sealed class ApiKeyHashingPepperRotationTests
{
    [Fact]
    public void ComputeHashWithVersion_returns_same_hash_for_same_plaintext_and_version()
    {
        var baseValue = TestValue('a');
        using var env = new PepperEnvironmentScope(new Dictionary<string, string?>
        {
            ["API_KEY_PEPPER"] = baseValue,
        });
        const string token = "rmv_live_same_hash_token";

        var first = ApiKeyHashing.ComputeHashWithVersion(token, 1);
        var second = ApiKeyHashing.ComputeHashWithVersion(token, 1);

        first.Should().Be(second);
    }

    [Fact]
    public void ComputeHashWithVersion_returns_different_hash_for_different_versions()
    {
        var baseValue = TestValue('a');
        var secondValue = TestValue('b');
        using var env = new PepperEnvironmentScope(new Dictionary<string, string?>
        {
            ["API_KEY_PEPPER"] = baseValue,
            ["API_KEY_PEPPER_V2"] = secondValue,
        });
        const string token = "rmv_live_different_version_hash_token";

        var firstVersion = ApiKeyHashing.ComputeHashWithVersion(token, 1);
        var secondVersion = ApiKeyHashing.ComputeHashWithVersion(token, 2);

        firstVersion.Should().NotBe(secondVersion);
    }

    [Fact]
    public void GetPepperForVersion_uses_versioned_env_var()
    {
        var baseValue = TestValue('a');
        var explicitFirstValue = TestValue('b');
        var secondValue = TestValue('c');
        using var env = new PepperEnvironmentScope(new Dictionary<string, string?>
        {
            ["API_KEY_PEPPER"] = baseValue,
            ["API_KEY_PEPPER_V1"] = explicitFirstValue,
            ["API_KEY_PEPPER_V2"] = secondValue,
        });
        var method = typeof(ApiKeyHashing).GetMethod(
            "GetPepperForVersion",
            BindingFlags.Static | BindingFlags.NonPublic);

        method.Should().NotBeNull();
        method!.Invoke(null, [1]).Should().Be(explicitFirstValue);
        method.Invoke(null, [2]).Should().Be(secondValue);

        Environment.SetEnvironmentVariable("API_KEY_PEPPER_V1", null);
        method.Invoke(null, [1]).Should().Be(baseValue);
    }

    private static string TestValue(char value) => new(value, 40);
}
