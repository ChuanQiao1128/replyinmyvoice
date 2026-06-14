using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.Common;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;

namespace ReplyInMyVoice.Application.UseCases.Billing;

public sealed class CreateCheckoutSessionHandler(
    IAppUserRepository appUsers,
    IStripeBillingClient stripeBillingClient,
    IUnitOfWork unitOfWork)
{
    public async Task<CheckoutSessionDto> HandleAsync(
        CreateCheckoutSessionCommand command,
        CancellationToken ct = default)
    {
        var externalAuthUserId = BillingUseCaseSupport.NormalizeExternalAuthUserId(command.ExternalAuthUserId);
        var email = BillingUseCaseSupport.NormalizeEmail(command.Email);
        var user = await appUsers.GetByExternalAuthUserIdAsync(externalAuthUserId, ct);
        var customerId = user?.StripeCustomerId;
        var session = await stripeBillingClient.CreateCheckoutSessionAsync(
            new StripeCheckoutSessionCreateRequest(
                customerId,
                string.IsNullOrWhiteSpace(customerId) ? email ?? user?.Email : null,
                command.Sku,
                externalAuthUserId),
            ct);

        await UpsertCheckoutUserAfterSessionCreatedAsync(
            user,
            externalAuthUserId,
            email,
            session.CustomerId,
            ct);

        return new CheckoutSessionDto(
            session.Url ?? throw new InvalidOperationException("stripe_checkout_url_missing"));
    }

    private async Task UpsertCheckoutUserAfterSessionCreatedAsync(
        AppUser? user,
        string externalAuthUserId,
        string? email,
        string? stripeCustomerId,
        CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        if (user is null)
        {
            await appUsers.AddAsync(new AppUser
            {
                ExternalAuthUserId = externalAuthUserId,
                Email = email,
                StripeCustomerId = stripeCustomerId,
                SubscriptionStatus = SubscriptionStatus.Inactive,
                CreatedAt = now,
                UpdatedAt = now,
            }, ct);
            await unitOfWork.SaveChangesAsync(ct);
            return;
        }

        var changed = false;
        if (!string.IsNullOrWhiteSpace(email) &&
            !string.Equals(user.Email, email, StringComparison.Ordinal))
        {
            user.Email = email;
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(user.StripeCustomerId) &&
            !string.IsNullOrWhiteSpace(stripeCustomerId))
        {
            user.StripeCustomerId = stripeCustomerId;
            changed = true;
        }

        if (!changed)
        {
            return;
        }

        user.UpdatedAt = now;
        await unitOfWork.SaveChangesAsync(ct);
    }
}
