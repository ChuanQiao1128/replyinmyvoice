using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.Common;
using ReplyInMyVoice.Application.UseCases.Account;

namespace ReplyInMyVoice.Application.UseCases.ApiKey;

public sealed class GetApiUsageSummaryHandler(
    IAppUserRepository appUsers,
    IUsagePeriodRepository usagePeriods,
    IRewriteCreditRepository credits,
    IApiKeyUsageRepository usage,
    IAccountUsagePlanProvider usagePlans)
{
    public async Task<ApiUsageSummaryDto?> HandleAsync(
        GetApiUsageSummaryQuery query,
        CancellationToken ct = default)
    {
        var externalAuthUserId = AccountUseCaseSupport.NormalizeExternalAuthUserId(query.ExternalAuthUserId);
        var account = await appUsers.GetByExternalAuthUserIdAsync(externalAuthUserId, ct);
        if (account is null)
        {
            return null;
        }

        var today = ApiUsageWindow.ToBusinessDate(query.Now);
        var yesterday = today.AddDays(-1);
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var last30Start = today.AddDays(-29);
        var summaryStart = monthStart < last30Start ? monthStart : last30Start;
        var rows = await usage.ListUsageRowsAsync(
            account.Id,
            ApiUsageWindow.ToBusinessDateStartUtc(summaryStart),
            ct);
        var eligibleRows = rows
            .Where(x => x.CreatedAt <= query.Now)
            .Select(x => new ApiUsageWindow.LocalUsageRow(
                ApiUsageWindow.ToBusinessDate(x.CreatedAt),
                x.StatusCode))
            .ToList();

        var usagePlan = usagePlans.GetUsagePlan(account);
        var period = await usagePeriods.GetByUserIdAndPeriodKeyAsync(
            account.Id,
            usagePlan.PeriodKey,
            ct);
        var userCredits = await credits.ListByUserIdAsync(account.Id, ct);
        var activeCredits = userCredits
            .Where(x => x.ExpiresAt is null || x.ExpiresAt > query.Now)
            .ToList();
        var creditRemaining = activeCredits.Sum(x => Math.Max(x.AmountGranted - x.AmountConsumed, 0));
        var creditUsed = activeCredits.Sum(x => Math.Max(x.AmountConsumed, 0));
        var used = (period?.UsedCount ?? 0) + creditUsed;
        var reserved = period?.ReservedCount ?? 0;
        var periodRemaining = Math.Max(usagePlan.QuotaLimit - (period?.UsedCount ?? 0) - reserved, 0);
        var remaining = periodRemaining + creditRemaining;

        return new ApiUsageSummaryDto(
            ApiUsageWindow.CountForDay(eligibleRows, today),
            ApiUsageWindow.CountForDay(eligibleRows, yesterday),
            ApiUsageWindow.CountForRange(eligibleRows, monthStart, today),
            eligibleRows.Count(x => x.Date >= last30Start && x.Date <= today),
            used + reserved + remaining,
            used,
            remaining,
            account.CurrentPeriodEnd);
    }
}
