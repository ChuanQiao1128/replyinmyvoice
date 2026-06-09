namespace ReplyInMyVoice.Application.Common;

public sealed record AdminPromoCodesListDto(
    IReadOnlyList<AdminPromoCodeDto> PromoCodes);

public sealed record AdminPromoCodeDto(
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
    DateTimeOffset? ArchivedAt,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record AdminPromoCodeDetailDto(
    AdminPromoCodeDto PromoCode,
    AdminPromoStatsDto Stats);

public sealed record AdminPromoStatsDto(
    int TotalRedemptions,
    int DistinctUsers,
    double ActivationRate,
    IReadOnlyList<AdminPromoDailyRedemptionsDto> DailyCurve,
    IReadOnlyList<AdminPromoIpHashClusterDto> IpHashClusters);

public sealed record AdminPromoDailyRedemptionsDto(
    string Date,
    int Redemptions);

public sealed record AdminPromoIpHashClusterDto(
    string IpHash,
    int Redemptions,
    int DistinctUsers,
    DateTimeOffset FirstRedeemedAt,
    DateTimeOffset LastRedeemedAt);

public sealed record AdminPromoMutationResultDto(
    AdminPromoResultKind Kind,
    AdminPromoCodeDto? Response,
    string? Detail)
{
    public static AdminPromoMutationResultDto Success(AdminPromoCodeDto response) =>
        new(AdminPromoResultKind.Success, response, null);

    public static AdminPromoMutationResultDto InvalidRequest(string detail) =>
        new(AdminPromoResultKind.InvalidRequest, null, detail);

    public static AdminPromoMutationResultDto DuplicateCode(string detail) =>
        new(AdminPromoResultKind.DuplicateCode, null, detail);

    public static AdminPromoMutationResultDto NotFound(string detail) =>
        new(AdminPromoResultKind.NotFound, null, detail);
}

public enum AdminPromoResultKind
{
    Success,
    InvalidRequest,
    DuplicateCode,
    NotFound,
}

public sealed record AdminPromoRedemptionRowDto(
    Guid UserId,
    Guid RewriteCreditId,
    string? RedeemIpHash,
    DateTimeOffset RedeemedAt);
