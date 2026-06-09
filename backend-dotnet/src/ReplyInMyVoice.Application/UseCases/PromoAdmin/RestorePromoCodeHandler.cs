using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.Common;

namespace ReplyInMyVoice.Application.UseCases.PromoAdmin;

public sealed class RestorePromoCodeHandler(
    IPromoAdminRepository promoAdmin,
    IUnitOfWork unitOfWork)
{
    public async Task<AdminPromoMutationResultDto> HandleAsync(
        RestorePromoCodeCommand command,
        CancellationToken ct = default)
    {
        var promoCode = await promoAdmin.GetPromoCodeByIdForUpdateAsync(command.PromoCodeId, ct);
        if (promoCode is null)
        {
            return AdminPromoMutationResultDto.NotFound("No promo code exists for the requested id.");
        }

        var changedFields = new List<string>();
        if (promoCode.ArchivedAt is not null)
        {
            promoCode.ArchivedAt = null;
            promoCode.UpdatedAt = command.Now;
            promoCode.RowVersion = Guid.NewGuid();
            changedFields.Add("archivedAt");
        }

        await PromoAdminUseCaseSupport.AddAuditAsync(
            promoAdmin,
            command.AdminExternalAuthUserId,
            command.AdminEmail,
            "promo_code_restore",
            promoCode.Id,
            changedFields,
            command.Now,
            ct);

        await unitOfWork.SaveChangesAsync(ct);
        return AdminPromoMutationResultDto.Success(PromoAdminUseCaseSupport.ToDto(promoCode, command.Now));
    }
}
