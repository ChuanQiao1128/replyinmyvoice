using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.Common;

namespace ReplyInMyVoice.Application.UseCases.PromoAdmin;

public sealed class GetPromoCodeDetailHandler(IPromoAdminRepository promoAdmin)
{
    public async Task<AdminPromoCodeDetailDto?> HandleAsync(
        GetPromoCodeDetailQuery query,
        CancellationToken ct = default)
    {
        var promoCode = await promoAdmin.GetPromoCodeByIdAsync(query.PromoCodeId, ct);
        if (promoCode is null)
        {
            return null;
        }

        var redemptionRows = await promoAdmin.ListAppliedRedemptionsAsync(query.PromoCodeId, ct);
        var creditIds = redemptionRows.Select(x => x.RewriteCreditId).ToHashSet();
        var activatedCreditIds = creditIds.Count == 0
            ? []
            : await promoAdmin.ListActivatedCreditIdsAsync(creditIds, ct);
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
            .Select(x => new AdminPromoDailyRedemptionsDto(
                x.Key.ToString("yyyy-MM-dd"),
                x.Count()))
            .ToList();

        var ipHashClusters = redemptionRows
            .Where(x => !string.IsNullOrWhiteSpace(x.RedeemIpHash))
            .GroupBy(x => x.RedeemIpHash!)
            .Select(x => new AdminPromoIpHashClusterDto(
                x.Key,
                x.Count(),
                x.Select(row => row.UserId).Distinct().Count(),
                x.Min(row => row.RedeemedAt),
                x.Max(row => row.RedeemedAt)))
            .OrderByDescending(x => x.Redemptions)
            .ThenBy(x => x.IpHash)
            .ToList();

        var stats = new AdminPromoStatsDto(
            redemptionRows.Count,
            distinctUsers,
            activationRate,
            dailyCurve,
            ipHashClusters);

        return new AdminPromoCodeDetailDto(
            PromoAdminUseCaseSupport.ToDto(promoCode, query.Now),
            stats);
    }
}
