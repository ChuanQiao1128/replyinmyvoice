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
using Microsoft.Extensions.Logging.Abstractions;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Functions.Functions;
using ReplyInMyVoice.Infrastructure.Data;
using ReplyInMyVoice.Infrastructure.Notifications;
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

    private WebApplicationFactory<Program> CreateFactory()
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
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
        var accountService = new AccountService(CreateContext);
        var notificationService = new NotificationService(
            notificationEmailProvider,
            NullLogger<NotificationService>.Instance);
        var billingSupportService = new BillingSupportService(
            CreateContext,
            notificationService);

        return new AccountHttpFunctions(
            BuildFunctionConfiguration(),
            accountService,
            billingSupportService);
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
        string SubscriptionStatus);

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
}
