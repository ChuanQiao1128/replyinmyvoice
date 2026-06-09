using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.Common;
using ReplyInMyVoice.Domain.Entities;

namespace ReplyInMyVoice.Application.UseCases.PromoAdmin;

public sealed class CreatePromoCodeHandler(
    IPromoAdminRepository promoAdmin,
    IUnitOfWork unitOfWork)
{
    public async Task<AdminPromoMutationResultDto> HandleAsync(
        CreatePromoCodeCommand command,
        CancellationToken ct = default)
    {
        var validation = PromoAdminUseCaseSupport.ValidateCreateRequest(command);
        if (validation is not null)
        {
            return AdminPromoMutationResultDto.InvalidRequest(validation);
        }

        var normalizedCode = PromoAdminUseCaseSupport.NormalizeCode(command.Code!)!;
        if (await promoAdmin.CodeExistsAsync(normalizedCode, ct))
        {
            return AdminPromoMutationResultDto.DuplicateCode("A promo code with that normalized code already exists.");
        }

        var promoCode = new PromoCode
        {
            Code = normalizedCode,
            DisplayCode = command.Code!.Trim(),
            Description = PromoAdminUseCaseSupport.NormalizeDescription(command.Description),
            Kind = PromoCodeKind.TrialCredits,
            CreditsGranted = command.CreditsGranted!.Value,
            GrantTtlDays = command.GrantTtlDays!.Value,
            ValidFrom = command.ValidFrom!.Value,
            ValidUntil = command.ValidUntil!.Value,
            MaxRedemptionsGlobal = command.MaxRedemptionsGlobal,
            MaxRedemptionsPerUser = command.MaxRedemptionsPerUser!.Value,
            RedemptionCount = 0,
            IsActive = true,
            CreatedAt = command.Now,
            UpdatedAt = command.Now,
        };

        await promoAdmin.AddPromoCodeAsync(promoCode, ct);
        await PromoAdminUseCaseSupport.AddAuditAsync(
            promoAdmin,
            command.AdminExternalAuthUserId,
            command.AdminEmail,
            "promo_code_create",
            promoCode.Id,
            [
                "code",
                "displayCode",
                "description",
                "creditsGranted",
                "grantTtlDays",
                "validFrom",
                "validUntil",
                "maxRedemptionsGlobal",
                "maxRedemptionsPerUser",
                "isActive",
            ],
            command.Now,
            ct);

        try
        {
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (promoAdmin.IsPromoCodeUniqueConstraintViolation(ex))
        {
            return AdminPromoMutationResultDto.DuplicateCode("A promo code with that normalized code already exists.");
        }

        return AdminPromoMutationResultDto.Success(PromoAdminUseCaseSupport.ToDto(promoCode, command.Now));
    }
}
