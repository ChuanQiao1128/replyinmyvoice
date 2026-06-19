using Microsoft.Extensions.Configuration;
using ReplyInMyVoice.Application.UseCases.Admin;
using ReplyInMyVoice.Application.UseCases.PromoAdmin;
using ReplyInMyVoice.Functions.Functions;
using ReplyInMyVoice.Infrastructure.Data;
using ReplyInMyVoice.Infrastructure.Providers;
using ReplyInMyVoice.Infrastructure.Repositories;
using ReplyInMyVoice.Infrastructure.Services;
using AppStripeRefundClient = ReplyInMyVoice.Application.Abstractions.IStripeRefundClient;
using AppStripeRefundRequest = ReplyInMyVoice.Application.Abstractions.StripeRefundRequest;
using AppStripeRefundResult = ReplyInMyVoice.Application.Abstractions.StripeRefundResult;
using LegacyStripeRefundClient = ReplyInMyVoice.Infrastructure.Services.IStripeRefundClient;
using LegacyStripeRefundRequest = ReplyInMyVoice.Infrastructure.Services.StripeRefundRequest;

namespace ReplyInMyVoice.Tests;

internal static class AdminHttpFunctionsTestFactory
{
    public static AdminHttpFunctions Create(
        IConfiguration configuration,
        Func<AppDbContext> dbContextFactory,
        LegacyStripeRefundClient? refundClient = null)
    {
        var db = dbContextFactory();
        var adminUsers = new AdminUserRepository(db);
        var adminStats = new AdminStatsRepository(db);
        var billingSupportRequests = new BillingSupportRequestRepository(db);
        var credits = new RewriteCreditRepository(db);
        var promoAdmin = new PromoAdminRepository(db);
        var deadLetters = new DeadLetterMessageRepository(db);
        var outboxMessages = new OutboxMessageRepository(db);
        var stripeEvents = new StripeEventRepository(db);
        var unitOfWork = new UnitOfWork(db);
        AppStripeRefundClient? applicationRefundClient = refundClient is null
            ? null
            : new LegacyStripeRefundClientAdapter(refundClient);

        return new AdminHttpFunctions(
            configuration,
            new GetAdminUsersHandler(adminUsers),
            new GetAdminUserDetailHandler(adminUsers),
            new GetAdminStatsHandler(
                adminStats,
                new TaxTurnoverSettingsProvider(configuration)),
            new DeleteAdminUserHandler(adminUsers, unitOfWork),
            new GrantCreditsHandler(adminUsers, unitOfWork),
            new GetBillingSupportQueueHandler(billingSupportRequests),
            new ResolveBillingSupportRequestHandler(billingSupportRequests, adminUsers, unitOfWork),
            new ExportAccountingRevenueHandler(credits),
            new ListDeadLettersHandler(deadLetters, adminUsers, unitOfWork),
            new GetDeadLetterDetailHandler(deadLetters, adminUsers, unitOfWork),
            new RequeueDeadLetterHandler(deadLetters, outboxMessages, stripeEvents, adminUsers, unitOfWork),
            new SetUserSuspensionHandler(adminUsers, unitOfWork),
            new IssueRefundHandler(adminUsers, credits, applicationRefundClient, unitOfWork),
            new CreatePromoCodeHandler(promoAdmin, unitOfWork),
            new ListPromoCodesHandler(promoAdmin),
            new GetPromoCodeDetailHandler(promoAdmin),
            new UpdatePromoCodeHandler(promoAdmin, unitOfWork),
            new SetPromoCodeActiveHandler(promoAdmin, unitOfWork),
            new ArchivePromoCodeHandler(promoAdmin, unitOfWork),
            new RestorePromoCodeHandler(promoAdmin, unitOfWork),
            new AdminRetryWebhookDeliveryHandler(new WebhookDeliveryRepository(db), unitOfWork));
    }

    private sealed class LegacyStripeRefundClientAdapter(LegacyStripeRefundClient refundClient) : AppStripeRefundClient
    {
        public async Task<AppStripeRefundResult> RefundPaymentAsync(
            AppStripeRefundRequest request,
            CancellationToken ct = default)
        {
            var refund = await refundClient.RefundPaymentAsync(
                new LegacyStripeRefundRequest(
                    request.PaymentIntentId,
                    request.Amount,
                    request.Currency,
                    request.IdempotencyKey,
                    request.TargetUserId),
                ct);

            return new AppStripeRefundResult(
                refund.RefundId,
                refund.PaymentIntentId,
                refund.Amount,
                refund.Currency,
                refund.Status);
        }
    }
}
