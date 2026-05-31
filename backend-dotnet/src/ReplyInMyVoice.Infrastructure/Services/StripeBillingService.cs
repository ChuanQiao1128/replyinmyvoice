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
    IConfiguration configuration,
    IStripeBillingClient? stripeBillingClient = null) : IStripeBillingService, IStripeRefundClient
{
    private const string LegacyPriceEnvVar = "STRIPE_PRICE_ID";
    internal const string PinnedStripeApiVersion = "2025-08-27.basil";
    private readonly IStripeBillingClient _stripeBillingClient = stripeBillingClient ?? new StripeBillingClient();

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
        var user = await GetCheckoutUserAsync(externalAuthUserId, cancellationToken);
        var appUrl = GetRequiredConfiguration("NEXT_PUBLIC_APP_URL").TrimEnd('/');
        var skuDefinition = ResolveSkuDefinition(sku);
        var priceEnvVar = skuDefinition?.PriceEnvVar ?? LegacyPriceEnvVar;
        var priceId = GetRequiredConfiguration(priceEnvVar);
        var mode = skuDefinition?.Mode ?? "subscription";
        var metadata = CreateCheckoutMetadata(externalAuthUserId, skuDefinition);
        var customerId = user?.StripeCustomerId;
        var session = await _stripeBillingClient.CreateCheckoutSessionAsync(
            new StripeCheckoutSessionCreateRequest(
                customerId,
                string.IsNullOrWhiteSpace(customerId) ? email ?? user?.Email : null,
                mode,
                priceId,
                appUrl,
                externalAuthUserId,
                metadata),
            stripeClient,
            cancellationToken);

        await UpsertCheckoutUserAfterSessionCreatedAsync(
            externalAuthUserId,
            email,
            session.CustomerId,
            cancellationToken);

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
        var session = await _stripeBillingClient.CreatePortalSessionAsync(
            user.StripeCustomerId,
            $"{appUrl}/app",
            stripeClient,
            cancellationToken);

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

        await _stripeBillingClient.CancelSubscriptionAsync(
            normalizedSubscriptionId,
            CreateStripeClient(),
            cancellationToken: cancellationToken);
    }

    public async Task<StripeRefundResult> RefundPaymentAsync(
        StripeRefundRequest request,
        CancellationToken cancellationToken)
    {
        return await _stripeBillingClient.RefundPaymentAsync(
            request,
            CreateStripeClient(),
            cancellationToken);
    }

    private async Task<AppUser?> GetCheckoutUserAsync(
        string externalAuthUserId,
        CancellationToken cancellationToken)
    {
        await using var db = dbContextFactory();
        return await db.AppUsers.AsNoTracking().SingleOrDefaultAsync(
            x => x.ExternalAuthUserId == externalAuthUserId,
            cancellationToken);
    }

    private async Task UpsertCheckoutUserAfterSessionCreatedAsync(
        string externalAuthUserId,
        string? email,
        string? stripeCustomerId,
        CancellationToken cancellationToken)
    {
        await using var db = dbContextFactory();
        var user = await db.AppUsers.AsTracking().SingleOrDefaultAsync(
            x => x.ExternalAuthUserId == externalAuthUserId,
            cancellationToken);
        var now = DateTimeOffset.UtcNow;
        if (user is null)
        {
            user = new AppUser
            {
                ExternalAuthUserId = externalAuthUserId,
                Email = email,
                StripeCustomerId = stripeCustomerId,
                SubscriptionStatus = SubscriptionStatus.Inactive,
                CreatedAt = now,
                UpdatedAt = now,
            };
            db.AppUsers.Add(user);
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        var changed = false;
        if (!string.IsNullOrWhiteSpace(email) && !string.Equals(user.Email, email, StringComparison.Ordinal))
        {
            user.Email = email;
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(user.StripeCustomerId) && !string.IsNullOrWhiteSpace(stripeCustomerId))
        {
            user.StripeCustomerId = stripeCustomerId;
            changed = true;
        }

        if (changed)
        {
            user.UpdatedAt = now;
            user.RowVersion = Guid.NewGuid();
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    internal static void EnsureStripeApiVersionPinned()
    {
        if (!string.Equals(StripeConfiguration.ApiVersion, PinnedStripeApiVersion, StringComparison.Ordinal))
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

public interface IStripeBillingClient
{
    Task<StripeCheckoutSessionResult> CreateCheckoutSessionAsync(
        StripeCheckoutSessionCreateRequest request,
        IStripeClient stripeClient,
        CancellationToken cancellationToken);

    Task<StripePortalSessionResult> CreatePortalSessionAsync(
        string customerId,
        string returnUrl,
        IStripeClient stripeClient,
        CancellationToken cancellationToken);

    Task CancelSubscriptionAsync(
        string stripeSubscriptionId,
        IStripeClient stripeClient,
        CancellationToken cancellationToken);

    Task<StripeRefundResult> RefundPaymentAsync(
        StripeRefundRequest request,
        IStripeClient stripeClient,
        CancellationToken cancellationToken);
}

public sealed class StripeBillingClient : IStripeBillingClient
{
    public async Task<StripeCheckoutSessionResult> CreateCheckoutSessionAsync(
        StripeCheckoutSessionCreateRequest request,
        IStripeClient stripeClient,
        CancellationToken cancellationToken)
    {
        var metadata = new Dictionary<string, string>(request.Metadata, StringComparer.Ordinal);
        var options = new Stripe.Checkout.SessionCreateOptions
        {
            Mode = request.Mode,
            ClientReferenceId = request.ExternalAuthUserId,
            SuccessUrl = $"{request.AppUrl}/app?checkout=success",
            CancelUrl = $"{request.AppUrl}/app?checkout=cancelled",
            LineItems =
            [
                new Stripe.Checkout.SessionLineItemOptions
                {
                    Price = request.PriceId,
                    Quantity = 1,
                }
            ],
            Metadata = metadata,
        };

        if (!string.IsNullOrWhiteSpace(request.CustomerId))
        {
            options.Customer = request.CustomerId;
        }
        else
        {
            options.CustomerEmail = request.CustomerEmail;
            if (request.Mode == "payment")
            {
                options.CustomerCreation = "always";
            }
        }

        if (request.Mode == "subscription")
        {
            options.SubscriptionData = new Stripe.Checkout.SessionSubscriptionDataOptions
            {
                Metadata = metadata,
            };
        }

        var sessionService = new Stripe.Checkout.SessionService(stripeClient);
        var session = await sessionService.CreateAsync(options, cancellationToken: cancellationToken);
        return new StripeCheckoutSessionResult(session.Url, session.CustomerId);
    }

    public async Task<StripePortalSessionResult> CreatePortalSessionAsync(
        string customerId,
        string returnUrl,
        IStripeClient stripeClient,
        CancellationToken cancellationToken)
    {
        var portalService = new Stripe.BillingPortal.SessionService(stripeClient);
        var session = await portalService.CreateAsync(new Stripe.BillingPortal.SessionCreateOptions
        {
            Customer = customerId,
            ReturnUrl = returnUrl,
        }, cancellationToken: cancellationToken);

        return new StripePortalSessionResult(session.Url);
    }

    public async Task CancelSubscriptionAsync(
        string stripeSubscriptionId,
        IStripeClient stripeClient,
        CancellationToken cancellationToken)
    {
        var subscriptionService = new SubscriptionService(stripeClient);
        await subscriptionService.CancelAsync(
            stripeSubscriptionId,
            new SubscriptionCancelOptions
            {
                InvoiceNow = false,
                Prorate = false,
            },
            cancellationToken: cancellationToken);
    }

    public async Task<StripeRefundResult> RefundPaymentAsync(
        StripeRefundRequest request,
        IStripeClient stripeClient,
        CancellationToken cancellationToken)
    {
        var refundService = new RefundService(stripeClient);
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
}

public sealed record CheckoutSkuDefinition(string Sku, string PriceEnvVar, string Mode, int Rewrites);

public sealed record StripeCheckoutSessionCreateRequest(
    string? CustomerId,
    string? CustomerEmail,
    string Mode,
    string PriceId,
    string AppUrl,
    string ExternalAuthUserId,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record StripeCheckoutSessionResult(string? Url, string? CustomerId);

public sealed record StripePortalSessionResult(string? Url);

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
