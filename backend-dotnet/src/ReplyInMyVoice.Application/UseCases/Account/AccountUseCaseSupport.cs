using System.Globalization;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;

namespace ReplyInMyVoice.Application.UseCases.Account;

internal static class AccountUseCaseSupport
{
    public static async Task<AppUser> GetOrCreateUserAsync(
        IAppUserRepository appUsers,
        IUnitOfWork unitOfWork,
        string externalAuthUserId,
        string? email,
        CancellationToken ct)
    {
        var normalizedExternalId = NormalizeExternalAuthUserId(externalAuthUserId);
        var normalizedEmail = NormalizeEmail(email);
        var user = await appUsers.GetByExternalAuthUserIdAsync(normalizedExternalId, ct);

        if (user is not null)
        {
            if (!string.IsNullOrWhiteSpace(normalizedEmail) && user.Email != normalizedEmail)
            {
                user.Email = normalizedEmail;
                user.UpdatedAt = DateTimeOffset.UtcNow;
                user.RowVersion = Guid.NewGuid();
                await unitOfWork.SaveChangesAsync(ct);
            }

            return user;
        }

        var now = DateTimeOffset.UtcNow;
        user = new AppUser
        {
            ExternalAuthUserId = normalizedExternalId,
            Email = normalizedEmail,
            SubscriptionStatus = SubscriptionStatus.Inactive,
            CreatedAt = now,
            UpdatedAt = now,
        };
        await appUsers.AddAsync(user, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return user;
    }

    public static string NormalizeExternalAuthUserId(string externalAuthUserId)
    {
        var normalized = externalAuthUserId.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 160)
        {
            throw new ArgumentException("A valid external auth user id is required.", nameof(externalAuthUserId));
        }

        return normalized;
    }

    public static bool IsErasedExternalAuthUserId(string externalAuthUserId) =>
        externalAuthUserId.StartsWith("erased:", StringComparison.Ordinal);

    public static string CreateErasedExternalAuthUserId(Guid userId) =>
        $"erased:{userId:N}";

    public static string CreateErasedChildToken(Guid id) =>
        $"erased:{id:N}";

    public static bool IsPaidApiSubscriptionStatus(SubscriptionStatus subscriptionStatus) =>
        subscriptionStatus is SubscriptionStatus.Active or SubscriptionStatus.Trialing or SubscriptionStatus.Testing;

    public static int? CalculateExpiresInDays(DateTimeOffset? expiresAt, DateTimeOffset now)
    {
        if (expiresAt is null)
        {
            return null;
        }

        return Math.Max(0, (int)Math.Ceiling((expiresAt.Value - now).TotalDays));
    }

    public static string LabelForCreditSource(string source) =>
        source == "PROMO" ? "Trial rewrites" : source;

    public static string FormatSubscriptionInvoiceDescription(
        DateTimeOffset? periodStart,
        DateTimeOffset? periodEnd)
    {
        if (periodStart is not null && periodEnd is not null)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"Subscription invoice {periodStart.Value:yyyy-MM-dd} - {periodEnd.Value:yyyy-MM-dd}");
        }

        return "Subscription invoice";
    }

    public static string FormatRefundDescription(string? sku) =>
        string.IsNullOrWhiteSpace(sku) ? "Pack refund" : $"Refund for {sku}";

    public static long? CalculateRefundAmount(
        long? originalAmount,
        int? originalGranted,
        int currentGranted)
    {
        if (originalAmount is not > 0 || originalGranted is not > 0 || currentGranted >= originalGranted.Value)
        {
            return null;
        }

        var refundedCredits = originalGranted.Value - currentGranted;
        var amount = Math.Round(
            originalAmount.Value * refundedCredits / (decimal)originalGranted.Value,
            MidpointRounding.AwayFromZero);
        return -(long)amount;
    }

    private static string? NormalizeEmail(string? email)
    {
        var normalized = email?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        return normalized.Length <= 320 ? normalized : normalized[..320];
    }
}
