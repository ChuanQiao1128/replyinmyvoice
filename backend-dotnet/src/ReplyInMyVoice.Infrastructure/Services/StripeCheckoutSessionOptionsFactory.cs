using Stripe.Checkout;

namespace ReplyInMyVoice.Infrastructure.Services;

public static class StripeCheckoutSessionOptionsFactory
{
    public static SessionCreateOptions Create(
        string mode,
        string customerId,
        string externalAuthUserId,
        string appUrl,
        string priceId,
        Dictionary<string, string> metadata,
        bool automaticTaxEnabled)
    {
        var options = new SessionCreateOptions
        {
            Mode = mode,
            Customer = customerId,
            ClientReferenceId = externalAuthUserId,
            SuccessUrl = $"{appUrl}/app?checkout=success",
            CancelUrl = $"{appUrl}/app?checkout=cancelled",
            LineItems =
            [
                new SessionLineItemOptions
                {
                    Price = priceId,
                    Quantity = 1,
                }
            ],
            Metadata = metadata,
        };

        if (mode == "subscription")
        {
            options.SubscriptionData = new SessionSubscriptionDataOptions
            {
                Metadata = metadata,
            };
        }

        if (automaticTaxEnabled)
        {
            options.AutomaticTax = new SessionAutomaticTaxOptions
            {
                Enabled = true,
            };
            options.BillingAddressCollection = "required";
            options.CustomerUpdate = new SessionCustomerUpdateOptions
            {
                Address = "auto",
                Name = "auto",
            };
            options.TaxIdCollection = new SessionTaxIdCollectionOptions
            {
                Enabled = true,
                Required = "if_supported",
            };
        }

        return options;
    }
}
