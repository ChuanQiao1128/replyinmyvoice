using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.Common;

namespace ReplyInMyVoice.Application.UseCases.PromoAdmin;

public sealed class UpdatePromoCodeHandler(
    IPromoAdminRepository promoAdmin,
    IUnitOfWork unitOfWork)
{
    public async Task<AdminPromoMutationResultDto> HandleAsync(
        UpdatePromoCodeCommand command,
        CancellationToken ct = default)
    {
        var promoCode = await promoAdmin.GetPromoCodeByIdForUpdateAsync(command.PromoCodeId, ct);
        if (promoCode is null)
        {
            return AdminPromoMutationResultDto.NotFound("No promo code exists for the requested id.");
        }

        var candidate = new AdminPromoCodeValidationCandidate(
            command.CreditsGranted ?? promoCode.CreditsGranted,
            command.GrantTtlDays ?? promoCode.GrantTtlDays,
            command.ValidFrom ?? promoCode.ValidFrom,
            command.ValidUntil ?? promoCode.ValidUntil,
            command.MaxRedemptionsGlobal ?? promoCode.MaxRedemptionsGlobal,
            command.MaxRedemptionsPerUser ?? promoCode.MaxRedemptionsPerUser);
        var validation = PromoAdminUseCaseSupport.ValidateCandidate(candidate);
        if (validation is not null)
        {
            return AdminPromoMutationResultDto.InvalidRequest(validation);
        }

        if (command.Description is not null && command.Description.Length > 200)
        {
            return AdminPromoMutationResultDto.InvalidRequest("Description must be 200 characters or fewer.");
        }

        var changedFields = new List<string>();
        PromoAdminUseCaseSupport.ApplyIfChanged(command.Description, promoCode.Description, "description", changedFields, value =>
            promoCode.Description = value);
        PromoAdminUseCaseSupport.ApplyIfChanged(command.CreditsGranted, promoCode.CreditsGranted, "creditsGranted", changedFields, value =>
            promoCode.CreditsGranted = value);
        PromoAdminUseCaseSupport.ApplyIfChanged(command.GrantTtlDays, promoCode.GrantTtlDays, "grantTtlDays", changedFields, value =>
            promoCode.GrantTtlDays = value);
        PromoAdminUseCaseSupport.ApplyIfChanged(command.ValidFrom, promoCode.ValidFrom, "validFrom", changedFields, value =>
            promoCode.ValidFrom = value);
        PromoAdminUseCaseSupport.ApplyIfChanged(command.ValidUntil, promoCode.ValidUntil, "validUntil", changedFields, value =>
            promoCode.ValidUntil = value);
        if (command.MaxRedemptionsGlobal is not null &&
            command.MaxRedemptionsGlobal != promoCode.MaxRedemptionsGlobal)
        {
            promoCode.MaxRedemptionsGlobal = command.MaxRedemptionsGlobal;
            changedFields.Add("maxRedemptionsGlobal");
        }
        PromoAdminUseCaseSupport.ApplyIfChanged(command.MaxRedemptionsPerUser, promoCode.MaxRedemptionsPerUser, "maxRedemptionsPerUser", changedFields, value =>
            promoCode.MaxRedemptionsPerUser = value);

        if (changedFields.Count == 0)
        {
            return AdminPromoMutationResultDto.InvalidRequest("At least one promo code field must change.");
        }

        promoCode.UpdatedAt = command.Now;
        promoCode.RowVersion = Guid.NewGuid();
        await PromoAdminUseCaseSupport.AddAuditAsync(
            promoAdmin,
            command.AdminExternalAuthUserId,
            command.AdminEmail,
            "promo_code_update",
            promoCode.Id,
            changedFields,
            command.Now,
            ct);

        await unitOfWork.SaveChangesAsync(ct);
        return AdminPromoMutationResultDto.Success(PromoAdminUseCaseSupport.ToDto(promoCode, command.Now));
    }
}
