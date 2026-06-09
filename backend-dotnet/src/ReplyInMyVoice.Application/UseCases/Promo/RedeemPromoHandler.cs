using System.Data;
using System.Text;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.Common;
using ReplyInMyVoice.Application.UseCases.Account;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;

namespace ReplyInMyVoice.Application.UseCases.Promo;

public sealed class RedeemPromoHandler(
    IAppUserRepository appUsers,
    IPromoCodeRepository promoCodes,
    IPromoCodeRedemptionRepository redemptions,
    IRewriteCreditRepository credits,
    IUnitOfWork unitOfWork)
{
    private const string PromoCreditSource = "PROMO";
    private const int DefaultIpVelocityMax24Hours = 5;
    private const int TransactionRetryCount = 8;

    public async Task<PromoRedeemResultDto> HandleAsync(
        RedeemPromoCommand command,
        CancellationToken ct = default)
    {
        var normalizedCode = NormalizeCode(command.RawCode);
        if (normalizedCode is null)
        {
            return PromoRedeemResultDto.InvalidCode();
        }

        var user = await AccountUseCaseSupport.GetOrCreateUserAsync(
            appUsers,
            unitOfWork,
            command.ExternalAuthUserId,
            command.Email,
            ct);
        var ipHash = NormalizeIpHash(command.IpHash);

        if (ipHash is not null)
        {
            var recentCount = await redemptions.CountAppliedByIpHashSinceAsync(
                ipHash,
                command.Now.AddHours(-24),
                ct);
            if (recentCount >= DefaultIpVelocityMax24Hours)
            {
                return PromoRedeemResultDto.IpVelocityBlocked();
            }
        }

        try
        {
            return await unitOfWork.ExecuteInTransactionAsync(
                transactionCt => RedeemInTransactionAsync(
                    user.Id,
                    normalizedCode,
                    ipHash,
                    command.Now,
                    transactionCt),
                IsolationLevel.Serializable,
                TransactionRetryCount,
                ct);
        }
        catch (Exception ex) when (redemptions.IsPromoCodeUserUniqueConstraintViolation(ex))
        {
            return PromoRedeemResultDto.AlreadyRedeemed();
        }
    }

    private async Task<PromoRedeemResultDto> RedeemInTransactionAsync(
        Guid userId,
        string normalizedCode,
        string? ipHash,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var promoCode = await promoCodes.GetByCodeAsync(normalizedCode, ct);
        if (promoCode is null || !promoCode.IsActive)
        {
            return PromoRedeemResultDto.InvalidCode();
        }

        if (now < promoCode.ValidFrom)
        {
            return PromoRedeemResultDto.InvalidCode();
        }

        if (now > promoCode.ValidUntil)
        {
            return PromoRedeemResultDto.Expired();
        }

        var alreadyRedeemed = await redemptions.ExistsForPromoCodeAndUserAsync(
            promoCode.Id,
            userId,
            ct);
        if (alreadyRedeemed)
        {
            return PromoRedeemResultDto.AlreadyRedeemed();
        }

        var rowsAffected = await promoCodes.TryIncrementRedemptionCountAsync(
            promoCode.Id,
            now,
            ct);
        if (rowsAffected == 0)
        {
            return await ResolveAtomicUpdateMissAsync(promoCode.Id, now, ct);
        }

        var expiresAt = now.AddDays(promoCode.GrantTtlDays);
        var credit = new RewriteCredit
        {
            UserId = userId,
            Source = PromoCreditSource,
            AmountGranted = promoCode.CreditsGranted,
            AmountConsumed = 0,
            GrantedAt = now,
            ExpiresAt = expiresAt,
        };
        await credits.AddAsync(credit, ct);

        await redemptions.AddAsync(new PromoCodeRedemption
        {
            PromoCodeId = promoCode.Id,
            UserId = userId,
            RewriteCreditId = credit.Id,
            CreditsGranted = promoCode.CreditsGranted,
            CodeSnapshot = promoCode.Code,
            RedeemIpHash = ipHash,
            Status = PromoCodeRedemptionStatus.Applied,
            RedeemedAt = now,
        }, ct);

        await unitOfWork.SaveChangesAsync(ct);
        return PromoRedeemResultDto.Success(promoCode.CreditsGranted, expiresAt, credit.Id);
    }

    private async Task<PromoRedeemResultDto> ResolveAtomicUpdateMissAsync(
        Guid promoCodeId,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var current = await promoCodes.GetByIdAsync(promoCodeId, ct);

        if (current is null || !current.IsActive || now < current.ValidFrom)
        {
            return PromoRedeemResultDto.InvalidCode();
        }

        if (now > current.ValidUntil)
        {
            return PromoRedeemResultDto.Expired();
        }

        if (current.MaxRedemptionsGlobal is not null &&
            current.RedemptionCount >= current.MaxRedemptionsGlobal)
        {
            return PromoRedeemResultDto.CapReached();
        }

        return PromoRedeemResultDto.InvalidCode();
    }

    private static string? NormalizeIpHash(string? ipHash)
    {
        var normalized = ipHash?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static string? NormalizeCode(string rawCode)
    {
        if (string.IsNullOrWhiteSpace(rawCode))
        {
            return null;
        }

        var builder = new StringBuilder(rawCode.Length);
        foreach (var ch in rawCode.Trim())
        {
            if (ch == '-' || char.IsWhiteSpace(ch))
            {
                continue;
            }

            var upper = char.ToUpperInvariant(ch);
            if (!IsAsciiLetterOrDigit(upper))
            {
                return null;
            }

            builder.Append(upper);
            if (builder.Length > 40)
            {
                return null;
            }
        }

        return builder.Length == 0 ? null : builder.ToString();
    }

    private static bool IsAsciiLetterOrDigit(char ch) =>
        ch is >= 'A' and <= 'Z' or >= '0' and <= '9';
}
