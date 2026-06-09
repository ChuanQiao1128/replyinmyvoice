namespace ReplyInMyVoice.Application.UseCases.Admin;

public sealed record ExportAccountingRevenueQuery(
    DateTimeOffset FromInclusive,
    DateTimeOffset ToExclusive,
    int PageSize = 500);
