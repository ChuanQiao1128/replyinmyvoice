using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ReplyInMyVoice.Application.UseCases.Account;
using ReplyInMyVoice.Application.UseCases.BillingSupport;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Functions.Functions;
using ReplyInMyVoice.Infrastructure.Data;
using ReplyInMyVoice.Infrastructure.Notifications;
using ReplyInMyVoice.Infrastructure.Repositories;
using ReplyInMyVoice.Infrastructure.Services;

namespace ReplyInMyVoice.Tests;

public sealed class AccountApiTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    static AccountApiTests()
    {
        Environment.SetEnvironmentVariable("DOTNET_USE_POLLING_FILE_WATCHER", "1");
        Environment.SetEnvironmentVariable("DOTNET_hostBuilder__reloadConfigOnChange", "false");
        Environment.SetEnvironmentVariable("ASPNETCORE_hostBuilder__reloadConfigOnChange", "false");
    }

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        await using var db = CreateContext();
        await db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task Me_upserts_authenticated_email_user_without_usage_side_effects()
    {
        await using var factory = CreateFactory();
        var client = CreateClient(factory);
        client.DefaultRequestHeaders.Add("X-External-User-Id", "entra_email_user");
        client.DefaultRequestHeaders.Add("X-User-Email", "teacher@example.com");

        var response = await client.GetAsync("/api/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AccountResponse>();
        body.Should().NotBeNull();
        body!.ExternalAuthUserId.Should().Be("entra_email_user");
        body.Email.Should().Be("teacher@example.com");
        body.Usage.Remaining.Should().Be(0);
        body.Usage.Exhausted.Should().BeTrue();

        await using var db = CreateContext();
        var user = await db.AppUsers.SingleAsync();
        user.ExternalAuthUserId.Should().Be("entra_email_user");
        user.Email.Should().Be("teacher@example.com");
        (await db.UsagePeriods.CountAsync()).Should().Be(0);
        (await db.RewriteAttempts.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Me_updates_email_for_existing_entra_subject_without_duplicate_user()
    {
        await using var factory = CreateFactory();
        var client = CreateClient(factory);
        client.DefaultRequestHeaders.Add("X-External-User-Id", "entra_same_subject");
        client.DefaultRequestHeaders.Add("X-User-Email", "first@example.com");
        (await client.GetAsync("/api/me")).StatusCode.Should().Be(HttpStatusCode.OK);

        client.DefaultRequestHeaders.Remove("X-User-Email");
        client.DefaultRequestHeaders.Add("X-User-Email", "updated@example.com");
        var response = await client.GetAsync("/api/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await using var db = CreateContext();
        var user = await db.AppUsers.SingleAsync();
        user.ExternalAuthUserId.Should().Be("entra_same_subject");
        user.Email.Should().Be("updated@example.com");
    }

    [Fact]
    public async Task Me_requires_authentication_and_does_not_create_user()
    {
        await using var factory = CreateFactory();
        var client = CreateClient(factory);

        var response = await client.GetAsync("/api/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await using var db = CreateContext();
        (await db.AppUsers.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task BillingSupportRequests_create_send_confirmation_and_scope_reads_to_caller()
    {
        var now = DateTimeOffset.Parse("2026-05-31T00:00:00Z");
        await using (var seedDb = CreateContext())
        {
            var user = new AppUser
            {
                ExternalAuthUserId = "entra_support_caller",
                Email = "caller@example.com",
                SubscriptionStatus = SubscriptionStatus.Inactive,
                CreatedAt = now,
                UpdatedAt = now,
            };
            seedDb.AppUsers.Add(user);
            seedDb.RewriteCredits.Add(new RewriteCredit
            {
                UserId = user.Id,
                Source = "PURCHASE",
                AmountGranted = 10,
                AmountConsumed = 0,
                GrantedAt = now,
                StripePaymentIntentId = "pi_support_caller",
                StripeSku = "quick_pack",
                StripeAmountTotal = 900,
                StripeCurrency = "nzd",
            });
            await seedDb.SaveChangesAsync();
        }

        var notificationProvider = new RecordingNotificationEmailProvider();
        var function = CreateAccountFunction(notificationProvider);
        var callerCreateRequest = CreateFunctionRequest("entra_support_caller", "caller@example.com", new
        {
            type = "refund",
            relatedPaymentIntentId = "pi_support_caller",
            message = "I was charged twice for the same rewrite pack. Please review the duplicate payment.",
        });

        var createResult = await function.CreateBillingSupportRequest(
            callerCreateRequest,
            CancellationToken.None);

        var createdResult = createResult.Should().BeOfType<CreatedResult>().Subject;
        createdResult.StatusCode.Should().Be((int)HttpStatusCode.Created);
        var created = createdResult.Value.Should().BeOfType<BillingSupportRequestResponse>().Subject;
        created.Should().NotBeNull();
        created.Type.Should().Be("refund");
        created.RelatedPaymentIntentId.Should().Be("pi_support_caller");
        created.Status.Should().Be("open");

        notificationProvider.SentMessages.Should().ContainSingle();
        notificationProvider.SentMessages[0].TemplateName.Should().Be("billing-support-request-received");
        notificationProvider.SentMessages[0].Recipient.Email.Should().Be("caller@example.com");

        var callerRead = await function.GetBillingSupportRequests(
            CreateFunctionRequest("entra_support_caller", "caller@example.com"),
            CancellationToken.None);
        var callerOk = callerRead.Should().BeOfType<OkObjectResult>().Subject;
        var callerRequests = callerOk.Value.Should().BeAssignableTo<IReadOnlyList<BillingSupportRequestResponse>>().Subject;
        callerRequests.Should().ContainSingle(x => x.Id == created.Id);

        var otherRead = await function.GetBillingSupportRequests(
            CreateFunctionRequest("entra_support_other", "other@example.com"),
            CancellationToken.None);
        var otherOk = otherRead.Should().BeOfType<OkObjectResult>().Subject;
        var otherRequests = otherOk.Value.Should().BeAssignableTo<IReadOnlyList<BillingSupportRequestResponse>>().Subject;
        otherRequests.Should().BeEmpty();

        await using var db = CreateContext();
        var stored = await db.BillingSupportRequests.SingleAsync();
        var storedUser = await db.AppUsers.SingleAsync(x => x.ExternalAuthUserId == "entra_support_caller");
        stored.UserId.Should().Be(storedUser.Id);
        stored.Message.Should().Contain("charged twice");
    }

    [Fact]
    public async Task Payments_returns_purchase_history_with_receipt_url()
    {
        var userId = Guid.NewGuid();
        await using (var db = CreateContext())
        {
            db.AppUsers.Add(new AppUser
            {
                Id = userId,
                ExternalAuthUserId = "entra_receipts",
                Email = "buyer@example.com",
                SubscriptionStatus = SubscriptionStatus.Inactive,
                CreatedAt = DateTimeOffset.Parse("2026-05-30T00:00:00Z"),
                UpdatedAt = DateTimeOffset.Parse("2026-05-30T00:00:00Z"),
            });
            db.RewriteCredits.Add(new RewriteCredit
            {
                UserId = userId,
                Source = "PURCHASE",
                AmountGranted = 10,
                AmountConsumed = 1,
                GrantedAt = DateTimeOffset.Parse("2026-05-30T10:00:00Z"),
                ExpiresAt = DateTimeOffset.Parse("2026-08-30T10:00:00Z"),
                StripeSku = "quick_pack",
                StripeAmountTotal = 250,
                StripeCurrency = "nzd",
                StripeReceiptUrl = "https://pay.stripe.test/receipts/quick-pack",
            });
            await db.SaveChangesAsync();
        }

        await using var factory = CreateFactory();
        var client = CreateClient(factory);
        client.DefaultRequestHeaders.Add("X-External-User-Id", "entra_receipts");
        client.DefaultRequestHeaders.Add("X-User-Email", "buyer@example.com");

        var response = await client.GetAsync("/api/me/payments");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<IReadOnlyList<AccountPaymentResponse>>();
        body.Should().NotBeNull();
        body.Should().ContainSingle();
        body![0].Sku.Should().Be("quick_pack");
        body[0].Amount.Should().Be(250);
        body[0].Currency.Should().Be("nzd");
        body[0].ReceiptUrl.Should().Be("https://pay.stripe.test/receipts/quick-pack");
        body[0].Remaining.Should().Be(9);
    }

    [Fact]
    public async Task BillingHistory_returns_pack_subscription_and_refund_rows()
    {
        var userId = Guid.NewGuid();
        var now = DateTimeOffset.Parse("2026-06-05T00:00:00Z");
        await using (var db = CreateContext())
        {
            db.AppUsers.Add(new AppUser
            {
                Id = userId,
                ExternalAuthUserId = "entra_billing_history",
                Email = "buyer@example.com",
                SubscriptionStatus = SubscriptionStatus.Active,
                StripeSubscriptionId = "sub_history",
                CreatedAt = now,
                UpdatedAt = now,
            });
            db.RewriteCredits.Add(new RewriteCredit
            {
                UserId = userId,
                Source = "PURCHASE",
                AmountGranted = 5,
                OriginalAmountGranted = 10,
                AmountConsumed = 0,
                GrantedAt = DateTimeOffset.Parse("2026-06-04T10:00:00Z"),
                ExpiresAt = DateTimeOffset.Parse("2026-09-04T10:00:00Z"),
                StripePaymentIntentId = "pi_billing_history_pack",
                StripeSku = "quick_pack",
                StripeAmountTotal = 1200,
                StripeCurrency = "nzd",
                StripeReceiptUrl = "https://pay.stripe.test/receipts/history-pack",
            });
            db.StripeInvoices.Add(new StripeInvoice
            {
                Id = "in_billing_history",
                UserId = userId,
                SubscriptionId = "sub_history",
                Status = "paid",
                AmountDue = 900,
                AmountPaid = 900,
                Currency = "nzd",
                PeriodStart = DateTimeOffset.Parse("2026-06-01T00:00:00Z"),
                PeriodEnd = DateTimeOffset.Parse("2026-07-01T00:00:00Z"),
                HostedInvoiceUrl = "https://invoice.stripe.test/history",
                CreatedAt = DateTimeOffset.Parse("2026-06-05T10:00:00Z"),
                UpdatedAt = DateTimeOffset.Parse("2026-06-05T10:00:00Z"),
            });
            await db.SaveChangesAsync();
        }

        await using var factory = CreateFactory();
        var client = CreateClient(factory);
        client.DefaultRequestHeaders.Add("X-External-User-Id", "entra_billing_history");
        client.DefaultRequestHeaders.Add("X-User-Email", "buyer@example.com");

        var response = await client.GetAsync("/api/me/billing/history");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<IReadOnlyList<BillingHistoryResponse>>();
        body.Should().NotBeNull();
        body.Should().HaveCount(3);
        body![0].Type.Should().Be("subscription");
        body[0].Amount.Should().Be(900);
        body[0].HostedInvoiceUrl.Should().Be("https://invoice.stripe.test/history");
        body.Should().ContainSingle(x =>
            x.Type == "pack" &&
            x.Amount == 1200 &&
            x.Currency == "nzd" &&
            x.ReceiptUrl == "https://pay.stripe.test/receipts/history-pack");
        body.Should().ContainSingle(x =>
            x.Type == "refund" &&
            x.Amount == -600 &&
            x.Status == "refunded");
    }

    [Fact]
    public async Task BillingHistory_function_requires_authentication()
    {
        var function = CreateAccountFunction(new RecordingNotificationEmailProvider());

        var result = await function.GetBillingHistory(
            new DefaultHttpContext().Request,
            CancellationToken.None);

        var problem = result.Should().BeOfType<ObjectResult>().Subject;
        problem.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task Me_includes_promo_block_and_trial_credit_label()
    {
        var userId = Guid.NewGuid();
        var promoCodeId = Guid.NewGuid();
        var creditId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.AddDays(45);

        await using (var db = CreateContext())
        {
            db.AppUsers.Add(new AppUser
            {
                Id = userId,
                ExternalAuthUserId = "entra_promo_api",
                Email = "promo-api@example.com",
                SubscriptionStatus = SubscriptionStatus.Inactive,
                CreatedAt = now,
                UpdatedAt = now,
            });
            db.PromoCodes.Add(new PromoCode
            {
                Id = promoCodeId,
                Code = "APICHECK",
                DisplayCode = "ApiCheck",
                Description = "Trial credits",
                Kind = PromoCodeKind.TrialCredits,
                CreditsGranted = 3,
                GrantTtlDays = 90,
                ValidFrom = now.AddDays(-1),
                ValidUntil = now.AddDays(30),
                MaxRedemptionsGlobal = 100,
                MaxRedemptionsPerUser = 1,
                RedemptionCount = 1,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
            });
            db.RewriteCredits.Add(new RewriteCredit
            {
                Id = creditId,
                UserId = userId,
                Source = "PROMO",
                AmountGranted = 3,
                AmountConsumed = 1,
                GrantedAt = now,
                ExpiresAt = expiresAt,
            });
            db.PromoCodeRedemptions.Add(new PromoCodeRedemption
            {
                PromoCodeId = promoCodeId,
                UserId = userId,
                RewriteCreditId = creditId,
                CreditsGranted = 3,
                CodeSnapshot = "APICHECK",
                Status = PromoCodeRedemptionStatus.Applied,
                RedeemedAt = now,
            });
            await db.SaveChangesAsync();
        }

        await using var factory = CreateFactory();
        var client = CreateClient(factory);
        client.DefaultRequestHeaders.Add("X-External-User-Id", "entra_promo_api");
        client.DefaultRequestHeaders.Add("X-User-Email", "promo-api@example.com");

        var response = await client.GetAsync("/api/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AccountResponse>();
        body.Should().NotBeNull();
        body!.Promo.Should().NotBeNull();
        body.Promo.HasRedeemed.Should().BeTrue();
        body.Promo.Eligible.Should().BeFalse();
        body.Promo.TrialRemaining.Should().Be(2);
        body.Promo.TrialExpiresAt.Should().BeCloseTo(expiresAt, TimeSpan.FromSeconds(1));
        body.Usage.Remaining.Should().Be(2);
        body.Usage.Exhausted.Should().BeFalse();
        body.Usage.Sources.Should().ContainSingle(x =>
            x.Source == "PROMO" &&
            x.Label == "Trial rewrites" &&
            x.Remaining == 2);
    }

    [Fact]
    public async Task Me_reports_exhausted_state_when_promo_credit_is_used()
    {
        var userId = Guid.NewGuid();
        var promoCodeId = Guid.NewGuid();
        var creditId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        await using (var db = CreateContext())
        {
            db.AppUsers.Add(new AppUser
            {
                Id = userId,
                ExternalAuthUserId = "entra_promo_exhausted",
                Email = "promo-exhausted@example.com",
                SubscriptionStatus = SubscriptionStatus.Inactive,
                CreatedAt = now,
                UpdatedAt = now,
            });
            db.PromoCodes.Add(new PromoCode
            {
                Id = promoCodeId,
                Code = "EXHAUSTEDCHECK",
                DisplayCode = "ExhaustedCheck",
                Description = "Trial credits",
                Kind = PromoCodeKind.TrialCredits,
                CreditsGranted = 3,
                GrantTtlDays = 90,
                ValidFrom = now.AddDays(-1),
                ValidUntil = now.AddDays(30),
                MaxRedemptionsGlobal = 100,
                MaxRedemptionsPerUser = 1,
                RedemptionCount = 1,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
            });
            db.RewriteCredits.Add(new RewriteCredit
            {
                Id = creditId,
                UserId = userId,
                Source = "PROMO",
                AmountGranted = 3,
                AmountConsumed = 3,
                GrantedAt = now,
                ExpiresAt = now.AddDays(30),
            });
            db.PromoCodeRedemptions.Add(new PromoCodeRedemption
            {
                PromoCodeId = promoCodeId,
                UserId = userId,
                RewriteCreditId = creditId,
                CreditsGranted = 3,
                CodeSnapshot = "EXHAUSTEDCHECK",
                Status = PromoCodeRedemptionStatus.Applied,
                RedeemedAt = now,
            });
            await db.SaveChangesAsync();
        }

        await using var factory = CreateFactory();
        var client = CreateClient(factory);
        client.DefaultRequestHeaders.Add("X-External-User-Id", "entra_promo_exhausted");
        client.DefaultRequestHeaders.Add("X-User-Email", "promo-exhausted@example.com");

        var response = await client.GetAsync("/api/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AccountResponse>();
        body.Should().NotBeNull();
        body!.Usage.Remaining.Should().Be(0);
        body.Usage.Exhausted.Should().BeTrue();
        body.Promo.HasRedeemed.Should().BeTrue();
        body.Promo.Eligible.Should().BeFalse();
        body.Promo.TrialRemaining.Should().Be(0);
    }

    private WebApplicationFactory<Program> CreateFactory()
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureLogging(logging => logging.ClearProviders());
                builder.ConfigureServices(services =>
                {
                    var dbOptions = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
                    if (dbOptions is not null)
                    {
                        services.Remove(dbOptions);
                    }

                    services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));
                });
            });
    }

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false,
        });

    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new AppDbContext(options);
    }

    private AccountHttpFunctions CreateAccountFunction(
        INotificationEmailProvider notificationEmailProvider)
    {
        var db = CreateContext();
        var unitOfWork = new UnitOfWork(db);
        var notificationService = new NotificationService(
            notificationEmailProvider,
            NullLogger<NotificationService>.Instance);

        return new AccountHttpFunctions(
            BuildFunctionConfiguration(),
            new GetAccountSummaryHandler(
                new AppUserRepository(db),
                new UsagePeriodRepository(db),
                new RewriteCreditRepository(db),
                new PromoCodeRedemptionRepository(db),
                new PromoCodeRepository(db),
                new AccountUsagePlanProvider(BuildFunctionConfiguration()),
                unitOfWork),
            new GetPurchaseHistoryHandler(
                new AppUserRepository(db),
                new RewriteCreditRepository(db),
                unitOfWork),
            new GetBillingHistoryHandler(
                new AppUserRepository(db),
                new RewriteCreditRepository(db),
                new StripeInvoiceRepository(db),
                unitOfWork),
            new GetBillingSupportRequestsHandler(new BillingSupportRepository(db)),
            new GetOrCreateUserHandler(
                new AppUserRepository(db),
                unitOfWork),
            new CreateBillingSupportRequestHandler(
                new BillingSupportRepository(db),
                unitOfWork),
            new DeleteAccountHandler(
                new AppUserRepository(db),
                new RewriteAttemptRepository(db),
                new UsagePeriodRepository(db),
                new UsageReservationRepository(db),
                new RewriteCreditRepository(db),
                new PromoCodeRedemptionRepository(db),
                new BillingSupportRequestRepository(db),
                unitOfWork),
            notificationService);
    }

    private static HttpRequest CreateFunctionRequest(
        string externalUserId,
        string email,
        object? body = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-External-User-Id"] = externalUserId;
        context.Request.Headers["X-User-Email"] = email;

        if (body is not null)
        {
            var json = JsonSerializer.Serialize(body);
            context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(json));
            context.Request.ContentType = "application/json";
        }

        return context.Request;
    }

    private static IConfiguration BuildFunctionConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ALLOW_HEADER_AUTH"] = "true",
            })
            .Build();

    private sealed record AccountResponse(
        Guid Id,
        string ExternalAuthUserId,
        string? Email,
        string SubscriptionStatus,
        AccountUsageResponse Usage,
        AccountPromoResponse Promo);

    private sealed record AccountUsageResponse(
        int Remaining,
        bool Exhausted,
        IReadOnlyList<AccountUsageSourceResponse> Sources);

    private sealed record AccountUsageSourceResponse(
        string Source,
        string Label,
        int Remaining);

    private sealed record AccountPromoResponse(
        bool HasRedeemed,
        bool Eligible,
        int TrialRemaining,
        DateTimeOffset? TrialExpiresAt);

    private sealed class RecordingNotificationEmailProvider : INotificationEmailProvider
    {
        public List<NotificationEmail> SentMessages { get; } = [];

        public Task<NotificationSendResult> SendAsync(
            NotificationEmail email,
            CancellationToken cancellationToken = default)
        {
            SentMessages.Add(email);
            return Task.FromResult(NotificationSendResult.Delivered("recording"));
        }
    }

    private sealed record AccountPaymentResponse(
        string? Sku,
        long? Amount,
        string? Currency,
        DateTimeOffset Date,
        DateTimeOffset? Expiry,
        int Remaining,
        string? ReceiptUrl);

    private sealed record BillingHistoryResponse(
        string Type,
        DateTimeOffset Date,
        string Description,
        long? Amount,
        string? Currency,
        string Status,
        string? ReceiptUrl,
        string? HostedInvoiceUrl);
}
