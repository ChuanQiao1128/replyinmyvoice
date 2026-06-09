using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.Common;

namespace ReplyInMyVoice.Application.UseCases.Admin;

public sealed class ExportAccountingRevenueHandler(IRewriteCreditRepository credits)
{
    private const int MaxAccountingExportPageSize = 1000;

    public async Task<AdminAccountingRevenueExportDto> HandleAsync(
        ExportAccountingRevenueQuery query,
        CancellationToken ct = default)
    {
        if (query.ToExclusive <= query.FromInclusive)
        {
            throw new ArgumentException("The export end date must be after the start date.", nameof(query));
        }

        var pageSize = Math.Clamp(query.PageSize, 1, MaxAccountingExportPageSize);
        var rows = await credits.ListAccountingRevenueRowsAsync(
            query.FromInclusive,
            query.ToExclusive,
            pageSize,
            ct);

        return new AdminAccountingRevenueExportDto(rows);
    }
}
