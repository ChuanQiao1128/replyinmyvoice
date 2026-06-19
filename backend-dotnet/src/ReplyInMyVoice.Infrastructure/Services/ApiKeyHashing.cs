using ReplyInMyVoice.Application.Common;

namespace ReplyInMyVoice.Infrastructure.Services;

public static class ApiKeyHashing
{
    public static string ComputeHash(string plaintext) =>
        ApiKeyCredential.ComputeHash(plaintext);

    public static string ComputeHashWithVersion(string plaintext, int version) =>
        ApiKeyCredential.ComputeHashWithVersion(plaintext, version);

    public static int CurrentPepperVersion => ApiKeyCredential.CurrentPepperVersion;

    public static bool TryGetPreviousPepperVersion(out int version) =>
        ApiKeyCredential.TryGetPreviousPepperVersion(out version);

    public static bool FixedTimeEquals(string expectedHash, string suppliedHash) =>
        ApiKeyCredential.FixedTimeEquals(expectedHash, suppliedHash);
}
