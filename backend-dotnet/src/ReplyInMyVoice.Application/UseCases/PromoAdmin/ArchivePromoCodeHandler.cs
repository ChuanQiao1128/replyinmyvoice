using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.Common;

namespace ReplyInMyVoice.Application.UseCases.PromoAdmin;

public sealed class ArchivePromoCodeHandler(
    IPromoAdminRepository promoAdmin,
    IUnitOfWork unitOfWork)
{
    public async Task<AdminPromoMutationResultDto> HandleAsync(
        ArchivePromoCodeCommand command,
        CancellationToken ct = default)
    {
        var promoCode = await promoAdmin.GetPromoCodeByIdForUpdateAsync(command.PromoCodeId, ct);
        if (promoCode is null)
        {
            return AdminPromoMutationResultDto.NotFound("No promo code exists for the requested id.");
        }

        var changedFields = new List<string>();
        if (promoCode.ArchivedAt is null)
        {
            promoCode.ArchivedAt = command.Now;
            changedFields.Add("archivedAt");
        }

        if (promoCode.IsActive)
        {
            promoCode.IsActive = false;
            changedFields.Add("isActive");
        }

        if (changedFields.Count > 0)
        {
            promoCode.UpdatedAt = command.Now;
            promoCode.RowVersion = Guid.NewGuid();
        }

        await PromoAdminUseCaseSupport.AddAuditAsync(
            promoAdmin,
            command.AdminExternalAuthUserId,
            command.AdminEmail,
            "promo_code_archive",
            promoCode.Id,
            changedFields,
            command.Now,
            ct);

        await unitOfWork.SaveChangesAsync(ct);
        return AdminPromoMutationResultDto.Success(PromoAdminUseCaseSupport.ToDto(promoCode, command.Now));
    }
}
