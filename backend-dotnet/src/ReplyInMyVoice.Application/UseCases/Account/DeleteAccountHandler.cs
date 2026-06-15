using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Domain.Enums;

namespace ReplyInMyVoice.Application.UseCases.Account;

public sealed class DeleteAccountHandler(
    IAppUserRepository appUsers,
    IRewriteAttemptRepository attempts,
    IUsagePeriodRepository usagePeriods,
    IUsageReservationRepository reservations,
    IRewriteCreditRepository credits,
    IPromoCodeRedemptionRepository promoRedemptions,
    IBillingSupportRequestRepository billingSupportRequests,
    IUnitOfWork unitOfWork,
    IStripeSubscriptionCancellationService? subscriptionCancellationService = null)
{
    public async Task HandleAsync(
        DeleteAccountCommand command,
        CancellationToken ct = default)
    {
        var normalizedExternalId = AccountUseCaseSupport.NormalizeExternalAuthUserId(command.ExternalAuthUserId);
        var account = await appUsers.GetByExternalAuthUserIdAsync(normalizedExternalId, ct);
        if (account is null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(account.StripeSubscriptionId) &&
            subscriptionCancellationService is not null)
        {
            await subscriptionCancellationService.CancelSubscriptionAsync(account.StripeSubscriptionId, ct);
        }

        await unitOfWork.ExecuteInTransactionAsync(async transactionCt =>
        {
            var user = await appUsers.GetByExternalAuthUserIdAsync(normalizedExternalId, transactionCt);
            if (user is null ||
                AccountUseCaseSupport.IsErasedExternalAuthUserId(user.ExternalAuthUserId))
            {
                return;
            }

            var now = DateTimeOffset.UtcNow;
            var userAttempts = await attempts.ListByUserIdAsync(user.Id, transactionCt);
            var userUsagePeriods = await usagePeriods.ListByUserIdAsync(user.Id, transactionCt);
            var userReservations = await reservations.ListByUserIdAsync(user.Id, transactionCt);
            var userCredits = await credits.ListByUserIdAsync(user.Id, transactionCt);
            var userPromoRedemptions = await promoRedemptions.ListByUserIdAsync(user.Id, transactionCt);
            var userBillingSupportRequests = await billingSupportRequests.ListByUserIdAsync(user.Id, transactionCt);

            user.ExternalAuthUserId = AccountUseCaseSupport.CreateErasedExternalAuthUserId(user.Id);
            user.Email = null;
            user.SubscriptionStatus = SubscriptionStatus.Canceled;
            user.CurrentPeriodEnd = null;
            user.UpdatedAt = now;

            foreach (var attempt in userAttempts)
            {
                attempt.IdempotencyKey = AccountUseCaseSupport.CreateErasedChildToken(attempt.Id);
                attempt.RequestHash = "erased";
                attempt.RequestJson = "{}";
                attempt.ResultJson = null;
                attempt.ErrorMessage = null;
                attempt.ExpiresAt = now;
                if (attempt.Status is RewriteAttemptStatus.Pending or RewriteAttemptStatus.Processing)
                {
                    attempt.Status = RewriteAttemptStatus.Failed;
                    attempt.ErrorCode = "account_erased";
                    attempt.CompletedAt = now;
                }

            }

            foreach (var period in userUsagePeriods)
            {
                period.PeriodKey = AccountUseCaseSupport.CreateErasedChildToken(period.Id);
                period.QuotaLimit = 0;
                period.UsedCount = 0;
                period.ReservedCount = 0;
                period.PeriodStart = null;
                period.PeriodEnd = null;
                period.UpdatedAt = now;
            }

            foreach (var reservation in userReservations)
            {
                reservation.Status = UsageReservationStatus.Released;
                reservation.FinalizedAt = null;
                reservation.ReleasedAt = now;
                reservation.ExpiresAt = now;
            }

            foreach (var credit in userCredits)
            {
                credit.Source = "ERASED";
                credit.AmountGranted = 0;
                credit.OriginalAmountGranted = null;
                credit.AmountConsumed = 0;
                credit.ExpiresAt = now;
            }

            foreach (var redemption in userPromoRedemptions)
            {
                redemption.RedeemIpHash = null;
            }

            foreach (var billingSupportRequest in userBillingSupportRequests)
            {
                billingSupportRequest.RelatedPaymentIntentId = null;
                billingSupportRequest.Message = "erased";
                billingSupportRequest.Status = BillingSupportRequestStatus.Resolved;
                billingSupportRequest.ResolvedAt ??= now;
                billingSupportRequest.UpdatedAt = now;
            }

            await unitOfWork.SaveChangesAsync(transactionCt);
        }, ct);
    }
}
