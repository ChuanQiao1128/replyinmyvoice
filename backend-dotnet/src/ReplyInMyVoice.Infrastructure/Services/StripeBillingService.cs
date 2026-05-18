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
    IConfiguration configuration) : IStripeBillingService
{
    public async Task<string> CreateCheckoutSessionUrlAsync(
        string externalAuthUserId,
        string? email,
        CancellationToken cancellationToken)
    {
        var stripeClient = CreateStripeClient();
        var user = await GetOrCreateUserAsync(externalAuthUserId, email, cancellationToken);
        var customerId = await GetOrCreateCustomerIdAsync(stripeClient, user, email, cancellationToken);
        var appUrl = GetRequiredConfiguration("NEXT_PUBLIC_APP_URL").TrimEnd('/');
        var priceId = GetRequiredConfiguration("STRIPE_PRICE_ID");

        var sessionService = new Stripe.Checkout.SessionService(stripeClient);
        var session = await sessionService.CreateAsync(new Stripe.Checkout.SessionCreateOptions
        {
            Mode = "subscription",
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
            Metadata = new Dictionary<string, string>
            {
                ["externalAuthUserId"] = externalAuthUserId,
            },
            SubscriptionData = new Stripe.Checkout.SessionSubscriptionDataOptions
            {
                Metadata = new Dictionary<string, string>
                {
                    ["externalAuthUserId"] = externalAuthUserId,
                },
            },
        }, cancellationToken: cancellationToken);

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

    private StripeClient CreateStripeClient() =>
        new(GetRequiredConfiguration("STRIPE_SECRET_KEY"));

    private string GetRequiredConfiguration(string key) =>
        configuration[key] ?? throw new InvalidOperationException($"{key}_missing");
}
