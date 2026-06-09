namespace ReplyInMyVoice.Application.Abstractions;

public interface ICreditExpiryNotifier
{
    Task<bool> TrySendCreditExpiringAsync(
        CreditExpiryNotificationRequest request,
        CancellationToken ct = default);
}

public sealed record CreditExpiryNotificationRequest(
    string RecipientEmail,
    int CreditsExpiring,
    DateTimeOffset ExpiresOnUtc);
