using Microsoft.Extensions.Configuration;
using ReplyInMyVoice.Application.UseCases.Admin;
using ReplyInMyVoice.Application.UseCases.PromoAdmin;
using ReplyInMyVoice.Functions.Functions;
using ReplyInMyVoice.Infrastructure.Data;
using ReplyInMyVoice.Infrastructure.Providers;
using ReplyInMyVoice.Infrastructure.Repositories;
using ReplyInMyVoice.Infrastructure.Services;

namespace ReplyInMyVoice.Tests;

internal static class AdminHttpFunctionsTestFactory
{
    public static AdminHttpFunctions Create(
        IConfiguration configuration,
        Func<AppDbContext> dbContextFactory,
        IStripeRefundClient? refundClient = null)
    {
        var db = dbContextFactory();
        var adminUsers = new AdminUserRepository(db);
        var adminStats = new AdminStatsRepository(db);
        var promoAdmin = new PromoAdminRepository(db);
        var unitOfWork = new UnitOfWork(db);

        return new AdminHttpFunctions(
            configuration,
            new AdminService(
                dbContextFactory,
                refundClient,
                new TaxTurnoverService(dbContextFactory, configuration),
                new AccountService(dbContextFactory, configuration)),
            new GetAdminUsersHandler(adminUsers),
            new GetAdminUserDetailHandler(adminUsers),
            new GetAdminStatsHandler(
                adminStats,
                new TaxTurnoverSettingsProvider(configuration)),
            new DeleteAdminUserHandler(adminUsers, unitOfWork),
            new GrantCreditsHandler(adminUsers, unitOfWork),
            new CreatePromoCodeHandler(promoAdmin, unitOfWork),
            new ListPromoCodesHandler(promoAdmin),
            new GetPromoCodeDetailHandler(promoAdmin),
            new UpdatePromoCodeHandler(promoAdmin, unitOfWork),
            new SetPromoCodeActiveHandler(promoAdmin, unitOfWork),
            new ArchivePromoCodeHandler(promoAdmin, unitOfWork),
            new RestorePromoCodeHandler(promoAdmin, unitOfWork));
    }
}
