using FluentAssertions;
using ReplyInMyVoice.Infrastructure.Services;
using Stripe;

namespace ReplyInMyVoice.Tests;

public sealed class StripeBillingServiceTests
{
    [Fact]
    public void EnsureStripeApiVersionPinned_throws_stripe_api_version_mismatch_when_configuration_drifts()
    {
        StripeConfiguration.ApiVersion.Should().Be(StripeBillingService.PinnedStripeApiVersion);

        var act = () => StripeBillingService.EnsureStripeApiVersionPinned("2024-01-01");

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("stripe_api_version_mismatch");
    }
}
