using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.Common;

namespace ReplyInMyVoice.Application.UseCases.PromoAdmin;

public sealed class ListPromoCodesHandler(IPromoAdminRepository promoAdmin)
{
    public async Task<AdminPromoCodesListDto> HandleAsync(
        ListPromoCodesQuery query,
        CancellationToken ct = default)
    {
        var promoCodes = await promoAdmin.ListPromoCodesAsync(ct);
        return new AdminPromoCodesListDto(
            promoCodes
                .OrderByDescending(x => x.CreatedAt)
                .ThenBy(x => x.DisplayCode ?? x.Code)
                .Select(x => PromoAdminUseCaseSupport.ToDto(x, query.Now))
                .ToList());
    }
}
