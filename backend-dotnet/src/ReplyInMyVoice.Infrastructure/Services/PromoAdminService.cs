using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Infrastructure.Data;

namespace ReplyInMyVoice.Infrastructure.Services;

public sealed class PromoAdminService(Func<AppDbContext> dbContextFactory)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<AdminPromoCodesListResponse> ListPromoCodesAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        await using var db = dbContextFactory();
        var rows = await db.PromoCodes
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var promoCodes = rows
            .OrderByDescending(x => x.CreatedAt)
            .ThenBy(x => x.DisplayCode ?? x.Code)
            .Select(x => ToResponse(x, now))
            .ToList();

        return new AdminPromoCodesListResponse(promoCodes);
    }

    public async Task<AdminPromoCodeDetailResponse?> GetPromoCodeDetailAsync(
        Guid promoCodeId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        await using var db = dbContextFactory();
        var promoCode = await db.PromoCodes
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == promoCodeId, cancellationToken);
        if (promoCode is null)
        {
            return null;
        }

        var redemptionRows = await db.PromoCodeRedemptions
            .AsNoTracking()
            .Where(x => x.PromoCodeId == promoCodeId && x.Status == PromoCodeRedemptionStatus.Applied)
            .Select(x => new AdminPromoRedemptionRow(
                x.UserId,
                x.RewriteCreditId,
                x.RedeemIpHash,
                x.RedeemedAt))
            .ToListAsync(cancellationToken);

        var creditIds = redemptionRows.Select(x => x.RewriteCreditId).ToHashSet();
        List<Guid> activatedCreditIds = creditIds.Count == 0
            ? []
            : await db.RewriteCredits
                .AsNoTracking()
                .Where(x => creditIds.Contains(x.Id) && x.AmountConsumed > 0)
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);
        var activatedCreditIdSet = activatedCreditIds.ToHashSet();

        var distinctUsers = redemptionRows.Select(x => x.UserId).Distinct().Count();
        var activatedUsers = redemptionRows
            .Where(x => activatedCreditIdSet.Contains(x.RewriteCreditId))
            .Select(x => x.UserId)
            .Distinct()
            .Count();
        var activationRate = distinctUsers == 0
            ? 0
            : activatedUsers / (double)distinctUsers;

        var dailyCurve = redemptionRows
            .GroupBy(x => x.RedeemedAt.UtcDateTime.Date)
            .OrderBy(x => x.Key)
            .Select(x => new AdminPromoDailyRedemptions(
                x.Key.ToString("yyyy-MM-dd"),
                x.Count()))
            .ToList();

        var ipHashClusters = redemptionRows
            .Where(x => !string.IsNullOrWhiteSpace(x.RedeemIpHash))
            .GroupBy(x => x.RedeemIpHash!)
            .Select(x => new AdminPromoIpHashCluster(
                x.Key,
                x.Count(),
                x.Select(row => row.UserId).Distinct().Count(),
                x.Min(row => row.RedeemedAt),
                x.Max(row => row.RedeemedAt)))
            .OrderByDescending(x => x.Redemptions)
            .ThenBy(x => x.IpHash)
            .ToList();

        var stats = new AdminPromoStats(
            redemptionRows.Count,
            distinctUsers,
            activationRate,
            dailyCurve,
            ipHashClusters);

        return new AdminPromoCodeDetailResponse(ToResponse(promoCode, now), stats);
    }

    public async Task<AdminPromoMutationResult> CreatePromoCodeAsync(
        string adminExternalAuthUserId,
        string? adminEmail,
        AdminPromoCodeCreateRequest request,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateCreateRequest(request);
        if (validation is not null)
        {
            return AdminPromoMutationResult.InvalidRequest(validation);
        }

        var normalizedCode = NormalizeCode(request.Code!)!;
        var displayCode = request.Code!.Trim();

        await using var db = dbContextFactory();
        var exists = await db.PromoCodes
            .AsNoTracking()
            .AnyAsync(x => x.Code == normalizedCode, cancellationToken);
        if (exists)
        {
            return AdminPromoMutationResult.DuplicateCode("A promo code with that normalized code already exists.");
        }

        var promoCode = new PromoCode
        {
            Code = normalizedCode,
            DisplayCode = displayCode,
            Description = NormalizeDescription(request.Description),
            Kind = PromoCodeKind.TrialCredits,
            CreditsGranted = request.CreditsGranted!.Value,
            GrantTtlDays = request.GrantTtlDays!.Value,
            ValidFrom = request.ValidFrom!.Value,
            ValidUntil = request.ValidUntil!.Value,
            MaxRedemptionsGlobal = request.MaxRedemptionsGlobal,
            MaxRedemptionsPerUser = request.MaxRedemptionsPerUser!.Value,
            RedemptionCount = 0,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.PromoCodes.Add(promoCode);
        AddAudit(
            db,
            adminExternalAuthUserId,
            adminEmail,
            "promo_code_create",
            promoCode.Id,
            [
                "code",
                "displayCode",
                "description",
                "creditsGranted",
                "grantTtlDays",
                "validFrom",
                "validUntil",
                "maxRedemptionsGlobal",
                "maxRedemptionsPerUser",
                "isActive",
            ],
            now);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsPromoCodeUniqueConstraintViolation(ex))
        {
            return AdminPromoMutationResult.DuplicateCode("A promo code with that normalized code already exists.");
        }

        return AdminPromoMutationResult.Success(ToResponse(promoCode, now));
    }

    public async Task<AdminPromoMutationResult> UpdatePromoCodeAsync(
        string adminExternalAuthUserId,
        string? adminEmail,
        Guid promoCodeId,
        AdminPromoCodeUpdateRequest request,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        await using var db = dbContextFactory();
        var promoCode = await db.PromoCodes
            .AsTracking()
            .SingleOrDefaultAsync(x => x.Id == promoCodeId, cancellationToken);
        if (promoCode is null)
        {
            return AdminPromoMutationResult.NotFound("No promo code exists for the requested id.");
        }

        var candidate = new AdminPromoCodeValidationCandidate(
            request.CreditsGranted ?? promoCode.CreditsGranted,
            request.GrantTtlDays ?? promoCode.GrantTtlDays,
            request.ValidFrom ?? promoCode.ValidFrom,
            request.ValidUntil ?? promoCode.ValidUntil,
            request.MaxRedemptionsGlobal ?? promoCode.MaxRedemptionsGlobal,
            request.MaxRedemptionsPerUser ?? promoCode.MaxRedemptionsPerUser);
        var validation = ValidateCandidate(candidate);
        if (validation is not null)
        {
            return AdminPromoMutationResult.InvalidRequest(validation);
        }

        if (request.Description is not null && request.Description.Length > 200)
        {
            return AdminPromoMutationResult.InvalidRequest("Description must be 200 characters or fewer.");
        }

        var changedFields = new List<string>();
        ApplyIfChanged(request.Description, promoCode.Description, "description", changedFields, value =>
            promoCode.Description = NormalizeDescription(value));
        ApplyIfChanged(request.CreditsGranted, promoCode.CreditsGranted, "creditsGranted", changedFields, value =>
            promoCode.CreditsGranted = value);
        ApplyIfChanged(request.GrantTtlDays, promoCode.GrantTtlDays, "grantTtlDays", changedFields, value =>
            promoCode.GrantTtlDays = value);
        ApplyIfChanged(request.ValidFrom, promoCode.ValidFrom, "validFrom", changedFields, value =>
            promoCode.ValidFrom = value);
        ApplyIfChanged(request.ValidUntil, promoCode.ValidUntil, "validUntil", changedFields, value =>
            promoCode.ValidUntil = value);
        if (request.MaxRedemptionsGlobal is not null &&
            request.MaxRedemptionsGlobal != promoCode.MaxRedemptionsGlobal)
        {
            promoCode.MaxRedemptionsGlobal = request.MaxRedemptionsGlobal;
            changedFields.Add("maxRedemptionsGlobal");
        }
        ApplyIfChanged(request.MaxRedemptionsPerUser, promoCode.MaxRedemptionsPerUser, "maxRedemptionsPerUser", changedFields, value =>
            promoCode.MaxRedemptionsPerUser = value);

        if (changedFields.Count == 0)
        {
            return AdminPromoMutationResult.InvalidRequest("At least one promo code field must change.");
        }

        promoCode.UpdatedAt = now;
        promoCode.RowVersion = Guid.NewGuid();
        AddAudit(
            db,
            adminExternalAuthUserId,
            adminEmail,
            "promo_code_update",
            promoCode.Id,
            changedFields,
            now);

        await db.SaveChangesAsync(cancellationToken);
        return AdminPromoMutationResult.Success(ToResponse(promoCode, now));
    }

    public async Task<AdminPromoMutationResult> SetPromoCodeActiveAsync(
        string adminExternalAuthUserId,
        string? adminEmail,
        Guid promoCodeId,
        bool isActive,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        await using var db = dbContextFactory();
        var promoCode = await db.PromoCodes
            .AsTracking()
            .SingleOrDefaultAsync(x => x.Id == promoCodeId, cancellationToken);
        if (promoCode is null)
        {
            return AdminPromoMutationResult.NotFound("No promo code exists for the requested id.");
        }

        var changedFields = new List<string>();
        if (promoCode.IsActive != isActive)
        {
            promoCode.IsActive = isActive;
            promoCode.UpdatedAt = now;
            promoCode.RowVersion = Guid.NewGuid();
            changedFields.Add("isActive");
        }

        AddAudit(
            db,
            adminExternalAuthUserId,
            adminEmail,
            isActive ? "promo_code_enable" : "promo_code_disable",
            promoCode.Id,
            changedFields,
            now);

        await db.SaveChangesAsync(cancellationToken);
        return AdminPromoMutationResult.Success(ToResponse(promoCode, now));
    }

    private static void ApplyIfChanged<T>(
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

    private static void ApplyIfChanged(
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

        apply(requested);
        changedFields.Add(fieldName);
    }

    private static AdminPromoCodeResponse ToResponse(PromoCode promoCode, DateTimeOffset now) =>
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
            ResolveStatus(promoCode, now),
            promoCode.CreatedAt,
            promoCode.UpdatedAt);

    private static string ResolveStatus(PromoCode promoCode, DateTimeOffset now)
    {
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

    private static string? ValidateCreateRequest(AdminPromoCodeCreateRequest request)
    {
        if (NormalizeCode(request.Code ?? string.Empty) is null)
        {
            return "A valid promo code is required.";
        }

        if (request.Code!.Trim().Length > 40)
        {
            return "Display code must be 40 characters or fewer.";
        }

        if (request.Description is not null && request.Description.Length > 200)
        {
            return "Description must be 200 characters or fewer.";
        }

        if (request.ValidFrom is null || request.ValidUntil is null)
        {
            return "Valid from and valid until are required.";
        }

        if (request.CreditsGranted is null ||
            request.GrantTtlDays is null ||
            request.MaxRedemptionsPerUser is null)
        {
            return "Credits granted, grant TTL days, and max redemptions per user are required.";
        }

        return ValidateCandidate(new AdminPromoCodeValidationCandidate(
            request.CreditsGranted.Value,
            request.GrantTtlDays.Value,
            request.ValidFrom.Value,
            request.ValidUntil.Value,
            request.MaxRedemptionsGlobal,
            request.MaxRedemptionsPerUser.Value));
    }

    private static string? ValidateCandidate(AdminPromoCodeValidationCandidate candidate)
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

    private static string? NormalizeCode(string rawCode)
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

    private static bool IsAsciiLetterOrDigit(char ch) =>
        ch is >= 'A' and <= 'Z' or >= '0' and <= '9';

    private static string? NormalizeDescription(string? description)
    {
        var normalized = description?.Trim();
        return string.IsNullOrWhiteSpace(normalized)
            ? null
            : normalized;
    }

    private static void AddAudit(
        AppDbContext db,
        string adminExternalAuthUserId,
        string? adminEmail,
        string action,
        Guid promoCodeId,
        IReadOnlyCollection<string> changedFields,
        DateTimeOffset now)
    {
        db.AdminAuditLogs.Add(new AdminAuditLog
        {
            AdminExternalAuthUserId = adminExternalAuthUserId.Trim(),
            AdminEmail = adminEmail?.Trim() ?? string.Empty,
            Action = action,
            TargetUserId = null,
            DetailsJson = JsonSerializer.Serialize(
                new AdminPromoAuditDetails(promoCodeId, changedFields.OrderBy(x => x, StringComparer.Ordinal).ToList()),
                JsonOptions),
            CreatedAt = now,
        });
    }

    private static bool IsPromoCodeUniqueConstraintViolation(DbUpdateException exception)
    {
        var message = exception.ToString();
        return message.Contains("IX_PromoCodes_Code", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("PromoCodes.Code", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record AdminPromoRedemptionRow(
        Guid UserId,
        Guid RewriteCreditId,
        string? RedeemIpHash,
        DateTimeOffset RedeemedAt);

    private sealed record AdminPromoCodeValidationCandidate(
        int CreditsGranted,
        int GrantTtlDays,
        DateTimeOffset ValidFrom,
        DateTimeOffset ValidUntil,
        int? MaxRedemptionsGlobal,
        int MaxRedemptionsPerUser);
}

public sealed record AdminPromoCodeCreateRequest(
    string? Code,
    string? Description,
    int? CreditsGranted,
    int? GrantTtlDays,
    DateTimeOffset? ValidFrom,
    DateTimeOffset? ValidUntil,
    int? MaxRedemptionsGlobal,
    int? MaxRedemptionsPerUser);

public sealed record AdminPromoCodeUpdateRequest(
    string? Description,
    int? CreditsGranted,
    int? GrantTtlDays,
    DateTimeOffset? ValidFrom,
    DateTimeOffset? ValidUntil,
    int? MaxRedemptionsGlobal,
    int? MaxRedemptionsPerUser);

public sealed record AdminPromoCodesListResponse(
    IReadOnlyList<AdminPromoCodeResponse> PromoCodes);

public sealed record AdminPromoCodeResponse(
    Guid Id,
    string Code,
    string? DisplayCode,
    string? Description,
    string Kind,
    int CreditsGranted,
    int GrantTtlDays,
    DateTimeOffset ValidFrom,
    DateTimeOffset ValidUntil,
    int? MaxRedemptionsGlobal,
    int MaxRedemptionsPerUser,
    int RedemptionCount,
    bool IsActive,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record AdminPromoCodeDetailResponse(
    AdminPromoCodeResponse PromoCode,
    AdminPromoStats Stats);

public sealed record AdminPromoStats(
    int TotalRedemptions,
    int DistinctUsers,
    double ActivationRate,
    IReadOnlyList<AdminPromoDailyRedemptions> DailyCurve,
    IReadOnlyList<AdminPromoIpHashCluster> IpHashClusters);

public sealed record AdminPromoDailyRedemptions(
    string Date,
    int Redemptions);

public sealed record AdminPromoIpHashCluster(
    string IpHash,
    int Redemptions,
    int DistinctUsers,
    DateTimeOffset FirstRedeemedAt,
    DateTimeOffset LastRedeemedAt);

public sealed record AdminPromoMutationResult(
    AdminPromoResultKind Kind,
    AdminPromoCodeResponse? Response,
    string? Detail)
{
    public static AdminPromoMutationResult Success(AdminPromoCodeResponse response) =>
        new(AdminPromoResultKind.Success, response, null);

    public static AdminPromoMutationResult InvalidRequest(string detail) =>
        new(AdminPromoResultKind.InvalidRequest, null, detail);

    public static AdminPromoMutationResult DuplicateCode(string detail) =>
        new(AdminPromoResultKind.DuplicateCode, null, detail);

    public static AdminPromoMutationResult NotFound(string detail) =>
        new(AdminPromoResultKind.NotFound, null, detail);
}

public enum AdminPromoResultKind
{
    Success,
    InvalidRequest,
    DuplicateCode,
    NotFound,
}

public sealed record AdminPromoAuditDetails(
    Guid PromoCodeId,
    IReadOnlyList<string> ChangedFields);
