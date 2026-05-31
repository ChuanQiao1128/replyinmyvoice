using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Infrastructure.Data;
using Stripe;
using Stripe.BillingPortal;
using Stripe.Checkout;

namespace ReplyInMyVoice.Infrastructure.Services;

public sealed class StripeBillingService(
    Func<AppDbContext> dbContextFactory,
    IConfiguration configuration) : IStripeBillingService, IStripeRefundClient
{
    private const string LegacyPriceEnvVar = "STRIPE_PRICE_ID";
    internal const string PinnedStripeApiVersion = "2025-08-27.basil";

    private static readonly IReadOnlyDictionary<string, CheckoutSkuDefinition> SkuDefinitions =
        new Dictionary<string, CheckoutSkuDefinition>(StringComparer.Ordinal)
        {
            ["quick_pack"] = new("quick_pack", "STRIPE_PRICE_QUICK_PACK_NZD", "payment", 10),
            ["value_pack"] = new("value_pack", "STRIPE_PRICE_VALUE_PACK_NZD", "payment", 30),
            ["pro_api"] = new("pro_api", "STRIPE_PRICE_PRO_API_MONTHLY_NZD", "subscription", 90),
            ["focus_pack"] = new("focus_pack", "STRIPE_PRICE_FOCUS_PACK_NZD", "payment", 20),
        };

    public async Task<string> CreateCheckoutSessionUrlAsync(
        string externalAuthUserId,
        string? email,
        string? sku,
        CancellationToken cancellationToken)
    {
        var stripeClient = CreateStripeClient();
        var user = await GetOrCreateUserAsync(externalAuthUserId, email, cancellationToken);
        var customerId = await GetOrCreateCustomerIdAsync(stripeClient, user, email, cancellationToken);
        var appUrl = GetRequiredConfiguration("NEXT_PUBLIC_APP_URL").TrimEnd('/');
        var skuDefinition = ResolveSkuDefinition(sku);
        var priceEnvVar = skuDefinition?.PriceEnvVar ?? LegacyPriceEnvVar;
        var priceId = GetRequiredConfiguration(priceEnvVar);
        var mode = skuDefinition?.Mode ?? "subscription";
        var metadata = CreateCheckoutMetadata(externalAuthUserId, skuDefinition);

        var sessionService = new Stripe.Checkout.SessionService(stripeClient);
        var options = new Stripe.Checkout.SessionCreateOptions
        {
            Mode = mode,
            Customer = customerId,
            ClientReferenceId = externalAuthUserId,
            SuccessUrl = $"{appUrl}/app?checkout=success",
            CancelUrl = $"{appUrl}/app?checkout=cancelled",
            LineItems =
            [
                new Stripe.Checkout.SessionLineItemOptions
                {
                    Price = priceId,
                    Quantity = 1,
                }
            ],
            Metadata = metadata,
        };

        if (mode == "subscription")
        {
            options.SubscriptionData = new Stripe.Checkout.SessionSubscriptionDataOptions
            {
                Metadata = metadata,
            };
        }

        var session = await sessionService.CreateAsync(options, cancellationToken: cancellationToken);

        return session.Url ?? throw new InvalidOperationException("stripe_checkout_url_missing");
    }

    public async Task<string> CreatePortalSessionUrlAsync(
        string externalAuthUserId,
        CancellationToken cancellationToken)
    {
        var stripeClient = CreateStripeClient();
        await using var db = dbContextFactory();
        var user = await db.AppUsers.AsNoTracking().SingleOrDefaultAsync(
            x => x.ExternalAuthUserId == externalAuthUserId,
            cancellationToken);

        if (string.IsNullOrWhiteSpace(user?.StripeCustomerId))
        {
            throw new InvalidOperationException("stripe_customer_missing");
        }

        var appUrl = GetRequiredConfiguration("NEXT_PUBLIC_APP_URL").TrimEnd('/');
        var portalService = new Stripe.BillingPortal.SessionService(stripeClient);
        var session = await portalService.CreateAsync(new Stripe.BillingPortal.SessionCreateOptions
        {
            Customer = user.StripeCustomerId,
            ReturnUrl = $"{appUrl}/app",
        }, cancellationToken: cancellationToken);

        return session.Url ?? throw new InvalidOperationException("stripe_portal_url_missing");
    }

    public async Task CancelSubscriptionAsync(
        string stripeSubscriptionId,
        CancellationToken cancellationToken)
    {
        var normalizedSubscriptionId = stripeSubscriptionId.Trim();
        if (string.IsNullOrWhiteSpace(normalizedSubscriptionId))
        {
            return;
        }

        var subscriptionService = new SubscriptionService(CreateStripeClient());
        await subscriptionService.CancelAsync(
            normalizedSubscriptionId,
            new SubscriptionCancelOptions
            {
                InvoiceNow = false,
                Prorate = false,
            },
            cancellationToken: cancellationToken);
    }

    public async Task<StripeRefundResult> RefundPaymentAsync(
        StripeRefundRequest request,
        CancellationToken cancellationToken)
    {
        var refundService = new RefundService(CreateStripeClient());
        var options = new RefundCreateOptions
        {
            PaymentIntent = request.PaymentIntentId,
            Amount = request.Amount,
            Currency = request.Currency,
            Metadata = new Dictionary<string, string>
            {
                ["source"] = "admin",
                ["targetUserId"] = request.TargetUserId.ToString("D"),
            },
        };

        var refund = await refundService.CreateAsync(
            options,
            new RequestOptions
            {
                IdempotencyKey = request.IdempotencyKey,
            },
            cancellationToken: cancellationToken);

        return new StripeRefundResult(
            refund.Id,
            refund.PaymentIntentId ?? request.PaymentIntentId,
            refund.Amount,
            refund.Currency ?? request.Currency,
            refund.Status);
    }

    private async Task<AppUser> GetOrCreateUserAsync(
        string externalAuthUserId,
        string? email,
        CancellationToken cancellationToken)
    {
        await using var db = dbContextFactory();
        var user = await db.AppUsers.AsTracking().SingleOrDefaultAsync(
            x => x.ExternalAuthUserId == externalAuthUserId,
            cancellationToken);
        if (user is not null)
        {
            if (!string.IsNullOrWhiteSpace(email) && user.Email != email)
            {
                user.Email = email;
                user.UpdatedAt = DateTimeOffset.UtcNow;
                user.RowVersion = Guid.NewGuid();
                await db.SaveChangesAsync(cancellationToken);
            }

            return user;
        }

        user = new AppUser
        {
            ExternalAuthUserId = externalAuthUserId,
            Email = email,
            SubscriptionStatus = SubscriptionStatus.Inactive,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        db.AppUsers.Add(user);
        await db.SaveChangesAsync(cancellationToken);
        return user;
    }

    private async Task<string> GetOrCreateCustomerIdAsync(
        IStripeClient stripeClient,
        AppUser user,
        string? email,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(user.StripeCustomerId))
        {
            return user.StripeCustomerId;
        }

        var customerService = new CustomerService(stripeClient);
        var customer = await customerService.CreateAsync(new CustomerCreateOptions
        {
            Email = email ?? user.Email,
            Metadata = new Dictionary<string, string>
            {
                ["externalAuthUserId"] = user.ExternalAuthUserId,
            },
        }, cancellationToken: cancellationToken);

        await using var db = dbContextFactory();
        var trackedUser = await db.AppUsers.AsTracking().SingleAsync(
            x => x.Id == user.Id,
            cancellationToken);
        trackedUser.StripeCustomerId = customer.Id;
        trackedUser.UpdatedAt = DateTimeOffset.UtcNow;
        trackedUser.RowVersion = Guid.NewGuid();
        await db.SaveChangesAsync(cancellationToken);

        return customer.Id;
    }

    internal static void EnsureStripeApiVersionPinned()
    {
        EnsureStripeApiVersionPinned(StripeConfiguration.ApiVersion);
    }

    internal static void EnsureStripeApiVersionPinned(string? actualApiVersion)
    {
        if (!string.Equals(actualApiVersion, PinnedStripeApiVersion, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("stripe_api_version_mismatch");
        }
    }

    private StripeClient CreateStripeClient()
    {
        EnsureStripeApiVersionPinned();
        return new StripeClient(GetRequiredConfiguration("STRIPE_SECRET_KEY"));
    }

    private string GetRequiredConfiguration(string key) =>
        configuration[key] ?? throw new InvalidOperationException($"{key}_missing");

    public static bool IsKnownSku(string sku) => SkuDefinitions.ContainsKey(sku);

    public static bool TryGetSkuDefinition(string? sku, out CheckoutSkuDefinition? definition)
    {
        if (!string.IsNullOrWhiteSpace(sku) &&
            SkuDefinitions.TryGetValue(sku, out var resolved))
        {
            definition = resolved;
            return true;
        }

        definition = null;
        return false;
    }

    private static CheckoutSkuDefinition? ResolveSkuDefinition(string? sku) =>
        TryGetSkuDefinition(sku, out var definition) ? definition : null;

    private static Dictionary<string, string> CreateCheckoutMetadata(
        string externalAuthUserId,
        CheckoutSkuDefinition? skuDefinition)
    {
        var metadata = new Dictionary<string, string>
        {
            ["externalAuthUserId"] = externalAuthUserId,
        };

        if (skuDefinition is not null)
        {
            metadata["sku"] = skuDefinition.Sku;
            metadata["rewrites"] = skuDefinition.Rewrites.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return metadata;
    }
}

public sealed record CheckoutSkuDefinition(string Sku, string PriceEnvVar, string Mode, int Rewrites);

public interface IStripeRefundClient
{
    Task<StripeRefundResult> RefundPaymentAsync(
        StripeRefundRequest request,
        CancellationToken cancellationToken);
}

public sealed record StripeRefundRequest(
    string PaymentIntentId,
    long Amount,
    string? Currency,
    string IdempotencyKey,
    Guid TargetUserId);

public sealed record StripeRefundResult(
    string RefundId,
    string PaymentIntentId,
    long Amount,
    string? Currency,
    string? Status);
