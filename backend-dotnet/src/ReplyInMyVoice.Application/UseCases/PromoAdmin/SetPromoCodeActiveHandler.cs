using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.Common;

namespace ReplyInMyVoice.Application.UseCases.PromoAdmin;

public sealed class SetPromoCodeActiveHandler(
    IPromoAdminRepository promoAdmin,
    IUnitOfWork unitOfWork)
{
    public async Task<AdminPromoMutationResultDto> HandleAsync(
        SetPromoCodeActiveCommand command,
        CancellationToken ct = default)
    {
        var promoCode = await promoAdmin.GetPromoCodeByIdForUpdateAsync(command.PromoCodeId, ct);
        if (promoCode is null)
        {
            return AdminPromoMutationResultDto.NotFound("No promo code exists for the requested id.");
        }

        var changedFields = new List<string>();
        if (promoCode.IsActive != command.IsActive)
        {
            promoCode.IsActive = command.IsActive;
            promoCode.UpdatedAt = command.Now;
            changedFields.Add("isActive");
        }

        await PromoAdminUseCaseSupport.AddAuditAsync(
            promoAdmin,
            command.AdminExternalAuthUserId,
            command.AdminEmail,
            command.IsActive ? "promo_code_enable" : "promo_code_disable",
            promoCode.Id,
            changedFields,
            command.Now,
            ct);

        await unitOfWork.SaveChangesAsync(ct);
        return AdminPromoMutationResultDto.Success(PromoAdminUseCaseSupport.ToDto(promoCode, command.Now));
    }
}
