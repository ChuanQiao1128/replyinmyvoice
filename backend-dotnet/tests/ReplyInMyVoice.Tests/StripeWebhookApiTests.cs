using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.UseCases.StripeEvent;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Functions.Functions;
using ReplyInMyVoice.Infrastructure.Data;
using ReplyInMyVoice.Infrastructure.Repositories;

namespace ReplyInMyVoice.Tests;

public sealed class StripeWebhookApiTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

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
    public async Task Stripe_webhook_marks_duplicate_event_as_not_processed()
    {
        var function = CreateFunction();
        var payload = JsonSerializer.Serialize(new { id = "evt_duplicate", type = "customer.subscription.updated" });

        var first = await function.Run(CreateFunctionRequest(payload), CancellationToken.None);
        var second = await function.Run(CreateFunctionRequest(payload), CancellationToken.None);

        first.Should().BeOfType<OkObjectResult>();
        var secondOk = second.Should().BeOfType<OkObjectResult>().Subject;
        var secondBody = JsonSerializer.Serialize(secondOk.Value);
        secondBody.Should().Contain("\"processed\":false");

        await using var db = CreateContext();
        (await db.StripeEvents.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Stripe_webhook_updates_subscription_entitlement_from_subscription_event()
    {
        await using var db = CreateContext();
        db.AppUsers.Add(new AppUser
        {
            ExternalAuthUserId = "clerk_billing",
            Email = "billing@example.com",
            StripeCustomerId = "cus_entitlement",
            SubscriptionStatus = SubscriptionStatus.Inactive,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var function = CreateFunction();
        var payload = JsonSerializer.Serialize(new
        {
            id = "evt_subscription_active",
            type = "customer.subscription.updated",
            data = new
            {
                @object = new
                {
                    id = "sub_entitlement",
                    customer = "cus_entitlement",
                    status = "active",
                    current_period_end = 1770000000
                }
            }
        });

        var response = await function.Run(CreateFunctionRequest(payload), CancellationToken.None);

        response.Should().BeOfType<OkObjectResult>();
        await using var verifyDb = CreateContext();
        var user = await verifyDb.AppUsers.SingleAsync();
        user.StripeSubscriptionId.Should().Be("sub_entitlement");
        user.SubscriptionStatus.Should().Be(SubscriptionStatus.Active);
        user.CurrentPeriodEnd.Should().NotBeNull();
    }

    [Fact]
    public async Task Webhook_invoice_payment_failed_persists_notification_outbox_row()
    {
        var userId = Guid.NewGuid();
        await using (var db = CreateContext())
        {
            db.AppUsers.Add(new AppUser
            {
                Id = userId,
                ExternalAuthUserId = "clerk_invoice_failed_outbox",
                Email = "invoice-failed-outbox@example.com",
                StripeCustomerId = "cus_invoice_failed_outbox",
                StripeSubscriptionId = "sub_invoice_failed_outbox",
                SubscriptionStatus = SubscriptionStatus.Active,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        var function = CreateFunction();
        var payload = JsonSerializer.Serialize(new
        {
            id = "evt_invoice_failed_outbox",
            type = "invoice.payment_failed",
            data = new
            {
                @object = new
                {
                    id = "in_invoice_failed_outbox",
                    customer = "cus_invoice_failed_outbox",
                    subscription = "sub_invoice_failed_outbox",
                    attempt_count = 2,
                    next_payment_attempt = 1770000000,
                    amount_due = 900,
                    amount_paid = 0,
                    currency = "nzd"
                }
            }
        });

        var response = await function.Run(CreateFunctionRequest(payload), CancellationToken.None);

        response.Should().BeOfType<OkObjectResult>();
        await using var verifyDb = CreateContext();
        var outbox = await verifyDb.OutboxMessages.SingleAsync();
        outbox.MessageType.Should().Be(StripeNotificationOutboxMessageTypes.PaymentFailed);
        outbox.Status.Should().Be(OutboxMessageStatus.Pending);
        outbox.MaxAttempts.Should().Be(10);
        outbox.CorrelationId.Should().Be("evt_invoice_failed_outbox");
        outbox.PayloadJson.Should().Contain(userId.ToString());
    }

    [Fact]
    public async Task Stripe_webhook_marks_deleted_subscription_as_canceled()
    {
        await using (var db = CreateContext())
        {
            db.AppUsers.Add(new AppUser
            {
                ExternalAuthUserId = "clerk_deleted_subscription",
                Email = "deleted@example.com",
                StripeCustomerId = "cus_deleted_subscription",
                StripeSubscriptionId = "sub_deleted_subscription",
                SubscriptionStatus = SubscriptionStatus.Active,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        var function = CreateFunction();
        var payload = JsonSerializer.Serialize(new
        {
            id = "evt_subscription_deleted",
            type = "customer.subscription.deleted",
            data = new
            {
                @object = new
                {
                    id = "sub_deleted_subscription",
                    customer = "cus_deleted_subscription",
                    status = "canceled",
                    current_period_end = 1770000000
                }
            }
        });

        var response = await function.Run(CreateFunctionRequest(payload), CancellationToken.None);

        response.Should().BeOfType<OkObjectResult>();
        await using var verifyDb = CreateContext();
        var user = await verifyDb.AppUsers.SingleAsync();
        user.SubscriptionStatus.ToString().Should().Be("Canceled");
    }

    [Fact]
    public async Task Stripe_webhook_rejects_missing_signature_in_production_when_secret_configured()
    {
        var function = CreateFunction("Production");
        var payload = JsonSerializer.Serialize(new { id = "evt_missing_signature", type = "customer.subscription.updated" });

        var response = await function.Run(CreateFunctionRequest(payload), CancellationToken.None);

        var objectResult = response.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Stripe_webhook_accepts_signed_event_in_production_when_secret_configured()
    {
        const string webhookSecret = "whsec_test_secret";
        var function = CreateFunction("Production", webhookSecret);
        var payload = JsonSerializer.Serialize(new
        {
            id = "evt_signed",
            @object = "event",
            api_version = "2025-08-27.basil",
            request = (string?)null,
            type = "customer.subscription.updated",
            data = new
            {
                @object = new
                {
                    id = "sub_signed",
                    @object = "subscription",
                    customer = "cus_signed",
                    status = "active",
                    current_period_end = 1770000000
                }
            }
        });

        var response = await function.Run(CreateSignedFunctionRequest(payload, webhookSecret), CancellationToken.None);

        response.Should().BeOfType<OkObjectResult>();
        await using var db = CreateContext();
        (await db.StripeEvents.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Stripe_webhook_rejects_wrong_secret_signature_in_production_without_persisting_event_or_grant()
    {
        const string webhookSecret = "whsec_test_secret";
        var function = CreateFunction("Production", webhookSecret);
        const string externalAuthUserId = "clerk_wrong_secret_signature";
        await SeedCreditCheckoutUserAsync(externalAuthUserId);
        var payload = CreatePaidCheckoutPayload("evt_wrong_secret_signature", externalAuthUserId);

        var response = await function.Run(
            CreateSignedFunctionRequest(payload, "whsec_different_secret"),
            CancellationToken.None);

        await AssertRejectedWithNoWebhookSideEffectsAsync(response);
    }

    [Fact]
    public async Task Stripe_webhook_rejects_tampered_payload_signature_in_production_without_persisting_event_or_grant()
    {
        const string webhookSecret = "whsec_test_secret";
        var function = CreateFunction("Production", webhookSecret);
        const string externalAuthUserId = "clerk_tampered_signature";
        await SeedCreditCheckoutUserAsync(externalAuthUserId);
        var originalPayload = CreatePaidCheckoutPayload("evt_tampered_signature", externalAuthUserId, rewriteCount: "10");
        var deliveredPayload = originalPayload.Replace("\"rewrites\":\"10\"", "\"rewrites\":\"30\"", StringComparison.Ordinal);

        var response = await function.Run(
            CreateSignedFunctionRequest(deliveredPayload, webhookSecret, payloadToSign: originalPayload),
            CancellationToken.None);

        await AssertRejectedWithNoWebhookSideEffectsAsync(response);
    }

    [Fact]
    public async Task Stripe_webhook_rejects_stale_timestamp_signature_in_production_without_persisting_event_or_grant()
    {
        const string webhookSecret = "whsec_test_secret";
        var function = CreateFunction("Production", webhookSecret);
        const string externalAuthUserId = "clerk_stale_timestamp_signature";
        await SeedCreditCheckoutUserAsync(externalAuthUserId);
        var payload = CreatePaidCheckoutPayload("evt_stale_timestamp_signature", externalAuthUserId);
        var staleTimestamp = DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeSeconds();

        var response = await function.Run(
            CreateSignedFunctionRequest(payload, webhookSecret, timestamp: staleTimestamp),
            CancellationToken.None);

        await AssertRejectedWithNoWebhookSideEffectsAsync(response);
    }

    [Fact]
    public async Task WebhookRefusesWhenSecretMissingInProduction()
    {
        await using (var db = CreateContext())
        {
            db.AppUsers.Add(new AppUser
            {
                ExternalAuthUserId = "clerk_unsigned_webhook",
                Email = "unsigned-webhook@example.com",
                SubscriptionStatus = SubscriptionStatus.Inactive,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        var function = CreateFunction("Production", webhookSecret: null);
        var payload = JsonSerializer.Serialize(new
        {
            id = "evt_unsigned_grant",
            type = "checkout.session.completed",
            data = new
            {
                @object = new
                {
                    client_reference_id = "clerk_unsigned_webhook",
                    customer = "cus_unsigned_webhook",
                    mode = "payment",
                    payment_status = "paid",
                    metadata = new
                    {
                        sku = "quick_pack",
                        rewrites = "10",
                        externalAuthUserId = "clerk_unsigned_webhook"
                    }
                }
            }
        });
        var request = CreateFunctionRequest(payload);

        var result = await function.Run(request, CancellationToken.None);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        await using var verifyDb = CreateContext();
        (await verifyDb.StripeEvents.CountAsync()).Should().Be(0);
        (await verifyDb.RewriteCredits.CountAsync()).Should().Be(0);
    }

    private StripeWebhookFunction CreateFunction(
        string environment = "Testing",
        string? webhookSecret = "whsec_test_secret")
    {
        var settings = new Dictionary<string, string?>();
        if (webhookSecret is not null)
        {
            settings["STRIPE_WEBHOOK_SECRET"] = webhookSecret;
        }

        return new StripeWebhookFunction(
            new ConfigurationBuilder()
                .AddInMemoryCollection(settings)
                .Build(),
            new TestHostEnvironment(environment),
            CreateWebhookHandler(CreateContext()),
            NullLogger<StripeWebhookFunction>.Instance);
    }

    private static ProcessStripeWebhookHandler CreateWebhookHandler(AppDbContext db) =>
        new(
            new StripeEventRepository(db),
            new AppUserRepository(db),
            new RewriteCreditRepository(db),
            new StripeInvoiceRepository(db),
            new OutboxMessageRepository(db),
            new UnitOfWork(db));

    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new AppDbContext(options);
    }

    private async Task SeedCreditCheckoutUserAsync(string externalAuthUserId)
    {
        await using var db = CreateContext();
        db.AppUsers.Add(new AppUser
        {
            ExternalAuthUserId = externalAuthUserId,
            Email = $"{externalAuthUserId}@example.com",
            SubscriptionStatus = SubscriptionStatus.Inactive,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    private async Task AssertRejectedWithNoWebhookSideEffectsAsync(IActionResult response)
    {
        var objectResult = response.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);

        await using var db = CreateContext();
        (await db.StripeEvents.CountAsync()).Should().Be(0);
        (await db.RewriteCredits.CountAsync()).Should().Be(0);
    }

    private static string CreatePaidCheckoutPayload(
        string eventId,
        string externalAuthUserId,
        string rewriteCount = "10")
    {
        return JsonSerializer.Serialize(new
        {
            id = eventId,
            @object = "event",
            api_version = "2025-08-27.basil",
            request = (string?)null,
            type = "checkout.session.completed",
            data = new
            {
                @object = new
                {
                    id = $"cs_{eventId}",
                    @object = "checkout.session",
                    client_reference_id = externalAuthUserId,
                    customer = $"cus_{eventId}",
                    mode = "payment",
                    payment_status = "paid",
                    payment_intent = $"pi_{eventId}",
                    amount_total = 1200,
                    currency = "nzd",
                    metadata = new
                    {
                        sku = "quick_pack",
                        rewrites = rewriteCount,
                        externalAuthUserId,
                    },
                },
            },
        });
    }

    private static HttpRequest CreateFunctionRequest(string payload)
    {
        var context = new DefaultHttpContext();
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(payload));
        context.Request.ContentType = "application/json";
        return context.Request;
    }

    private static HttpRequest CreateSignedFunctionRequest(
        string payload,
        string webhookSecret,
        long? timestamp = null,
        string? payloadToSign = null)
    {
        var request = CreateFunctionRequest(payload);
        var signatureTimestamp = timestamp ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var signedPayload = $"{signatureTimestamp}.{payloadToSign ?? payload}";
        var hash = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(webhookSecret),
            Encoding.UTF8.GetBytes(signedPayload));
        request.Headers["Stripe-Signature"] = $"t={signatureTimestamp},v1={Convert.ToHexString(hash).ToLowerInvariant()}";
        return request;
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "ReplyInMyVoice.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

}
