using System.Text;
using System.Text.Json;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.Common;
using ReplyInMyVoice.Domain.Entities;

namespace ReplyInMyVoice.Application.UseCases.PromoAdmin;

internal static class PromoAdminUseCaseSupport
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static AdminPromoCodeDto ToDto(PromoCode promoCode, DateTimeOffset now) =>
        new(
            promoCode.Id,
            promoCode.Code,
            promoCode.DisplayCode,
            promoCode.Description,
            promoCode.Kind.ToString(),
            promoCode.CreditsGranted,
            promoCode.GrantTtlDays,
            promoCode.ValidFrom,
            promoCode.ValidUntil,
            promoCode.MaxRedemptionsGlobal,
            promoCode.MaxRedemptionsPerUser,
            promoCode.RedemptionCount,
            promoCode.IsActive,
            promoCode.ArchivedAt,
            ResolveStatus(promoCode, now),
            promoCode.CreatedAt,
            promoCode.UpdatedAt);

    public static string ResolveStatus(PromoCode promoCode, DateTimeOffset now)
    {
        if (promoCode.ArchivedAt is not null)
        {
            return "archived";
        }

        if (!promoCode.IsActive)
        {
            return "disabled";
        }

        if (now < promoCode.ValidFrom)
        {
            return "pending";
        }

        if (now > promoCode.ValidUntil)
        {
            return "expired";
        }

        if (promoCode.MaxRedemptionsGlobal is not null &&
            promoCode.RedemptionCount >= promoCode.MaxRedemptionsGlobal)
        {
            return "exhausted";
        }

        return "active";
    }

    public static string? ValidateCreateRequest(CreatePromoCodeCommand command)
    {
        if (NormalizeCode(command.Code ?? string.Empty) is null)
        {
            return "A valid promo code is required.";
        }

        if (command.Code!.Trim().Length > 40)
        {
            return "Display code must be 40 characters or fewer.";
        }

        if (command.Description is not null && command.Description.Length > 200)
        {
            return "Description must be 200 characters or fewer.";
        }

        if (command.ValidFrom is null || command.ValidUntil is null)
        {
            return "Valid from and valid until are required.";
        }

        if (command.CreditsGranted is null ||
            command.GrantTtlDays is null ||
            command.MaxRedemptionsPerUser is null)
        {
            return "Credits granted, grant TTL days, and max redemptions per user are required.";
        }

        return ValidateCandidate(new AdminPromoCodeValidationCandidate(
            command.CreditsGranted.Value,
            command.GrantTtlDays.Value,
            command.ValidFrom.Value,
            command.ValidUntil.Value,
            command.MaxRedemptionsGlobal,
            command.MaxRedemptionsPerUser.Value));
    }

    public static string? ValidateCandidate(AdminPromoCodeValidationCandidate candidate)
    {
        if (candidate.CreditsGranted <= 0)
        {
            return "Credits granted must be greater than zero.";
        }

        if (candidate.GrantTtlDays <= 0)
        {
            return "Grant TTL days must be greater than zero.";
        }

        if (candidate.MaxRedemptionsPerUser < 1)
        {
            return "Max redemptions per user must be at least one.";
        }

        if (candidate.MaxRedemptionsGlobal is <= 0)
        {
            return "Max redemptions global must be greater than zero when provided.";
        }

        if (candidate.ValidUntil <= candidate.ValidFrom)
        {
            return "Valid until must be after valid from.";
        }

        return null;
    }

    public static void ApplyIfChanged<T>(
        T? requested,
        T current,
        string fieldName,
        ICollection<string> changedFields,
        Action<T> apply)
        where T : struct
    {
        if (requested is null || EqualityComparer<T>.Default.Equals(requested.Value, current))
        {
            return;
        }

        apply(requested.Value);
        changedFields.Add(fieldName);
    }

    public static void ApplyIfChanged(
        string? requested,
        string? current,
        string fieldName,
        ICollection<string> changedFields,
        Action<string?> apply)
    {
        if (requested is null)
        {
            return;
        }

        var normalized = NormalizeDescription(requested);
        if (string.Equals(normalized, current, StringComparison.Ordinal))
        {
            return;
        }

        apply(normalized);
        changedFields.Add(fieldName);
    }

    public static string? NormalizeCode(string rawCode)
    {
        if (string.IsNullOrWhiteSpace(rawCode))
        {
            return null;
        }

        var builder = new StringBuilder(rawCode.Length);
        foreach (var ch in rawCode.Trim())
        {
            if (ch == '-' || char.IsWhiteSpace(ch))
            {
                continue;
            }

            var upper = char.ToUpperInvariant(ch);
            if (!IsAsciiLetterOrDigit(upper))
            {
                return null;
            }

            builder.Append(upper);
            if (builder.Length > 40)
            {
                return null;
            }
        }

        return builder.Length == 0 ? null : builder.ToString();
    }

    public static string? NormalizeDescription(string? description)
    {
        var normalized = description?.Trim();
        return string.IsNullOrWhiteSpace(normalized)
            ? null
            : normalized;
    }

    public static async Task AddAuditAsync(
        IPromoAdminRepository promoAdmin,
        string adminExternalAuthUserId,
        string? adminEmail,
        string action,
        Guid promoCodeId,
        IReadOnlyCollection<string> changedFields,
        DateTimeOffset now,
        CancellationToken ct)
    {
        await promoAdmin.AddAuditLogAsync(new AdminAuditLog
        {
            AdminExternalAuthUserId = adminExternalAuthUserId.Trim(),
            AdminEmail = adminEmail?.Trim() ?? string.Empty,
            Action = action,
            TargetUserId = null,
            DetailsJson = JsonSerializer.Serialize(
                new AdminPromoAuditDetails(promoCodeId, changedFields.OrderBy(x => x, StringComparer.Ordinal).ToList()),
                JsonOptions),
            CreatedAt = now,
        }, ct);
    }

    private static bool IsAsciiLetterOrDigit(char ch) =>
        ch is >= 'A' and <= 'Z' or >= '0' and <= '9';

    private sealed record AdminPromoAuditDetails(
        Guid PromoCodeId,
        IReadOnlyList<string> ChangedFields);
}

internal sealed record AdminPromoCodeValidationCandidate(
    int CreditsGranted,
    int GrantTtlDays,
    DateTimeOffset ValidFrom,
    DateTimeOffset ValidUntil,
    int? MaxRedemptionsGlobal,
    int MaxRedemptionsPerUser);
