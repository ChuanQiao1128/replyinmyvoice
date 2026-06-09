namespace ReplyInMyVoice.Infrastructure.Services;

public static class ExternalAuthUserId
{
    public static bool IsErasedExternalAuthUserId(string externalAuthUserId) =>
        externalAuthUserId.StartsWith("erased:", StringComparison.Ordinal);
}
