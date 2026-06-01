using FluentAssertions;
using ReplyInMyVoice.Infrastructure.Services;

namespace ReplyInMyVoice.Tests;

public sealed class StripeCheckoutSessionOptionsFactoryTests
{
    [Fact]
    public void Create_keeps_tax_fields_unset_when_automatic_tax_flag_is_off()
    {
        var options = StripeCheckoutSessionOptionsFactory.Create(
            mode: "payment",
            customerId: "cus_test",
            externalAuthUserId: "clerk_test",
            appUrl: "https://replyinmyvoice.test",
            priceId: "price_test",
            metadata: new Dictionary<string, string>
            {
                ["sku"] = "quick_pack",
            },
            automaticTaxEnabled: false);

        options.AutomaticTax.Should().BeNull();
        options.BillingAddressCollection.Should().BeNull();
        options.CustomerUpdate.Should().BeNull();
        options.TaxIdCollection.Should().BeNull();
        options.Mode.Should().Be("payment");
        options.Customer.Should().Be("cus_test");
        options.ClientReferenceId.Should().Be("clerk_test");
        options.SuccessUrl.Should().Be("https://replyinmyvoice.test/app?checkout=success");
        options.CancelUrl.Should().Be("https://replyinmyvoice.test/app?checkout=cancelled");
    }

    [Fact]
    public void Create_adds_required_checkout_tax_collection_when_automatic_tax_flag_is_on()
    {
        var options = StripeCheckoutSessionOptionsFactory.Create(
            mode: "subscription",
            customerId: "cus_tax",
            externalAuthUserId: "clerk_tax",
            appUrl: "https://replyinmyvoice.test",
            priceId: "price_tax",
            metadata: new Dictionary<string, string>
            {
                ["sku"] = "pro_api",
            },
            automaticTaxEnabled: true);

        options.AutomaticTax.Should().NotBeNull();
        options.AutomaticTax!.Enabled.Should().BeTrue();
        options.BillingAddressCollection.Should().Be("required");
        options.CustomerUpdate.Should().NotBeNull();
        options.CustomerUpdate!.Address.Should().Be("auto");
        options.CustomerUpdate.Name.Should().Be("auto");
        options.TaxIdCollection.Should().NotBeNull();
        options.TaxIdCollection!.Enabled.Should().BeTrue();
        options.TaxIdCollection.Required.Should().Be("if_supported");
        options.SubscriptionData.Should().NotBeNull();
        options.SubscriptionData!.Metadata.Should().ContainKey("sku").WhoseValue.Should().Be("pro_api");
    }
}
