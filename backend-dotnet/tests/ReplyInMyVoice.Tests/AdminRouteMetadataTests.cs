using System.Reflection;
using FluentAssertions;
using Microsoft.Azure.Functions.Worker;
using ReplyInMyVoice.Functions.Functions;

namespace ReplyInMyVoice.Tests;

public sealed class AdminRouteMetadataTests
{
    [Fact]
    public void AdminHttpFunctions_use_console_route_prefix()
    {
        var routes = typeof(AdminHttpFunctions)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .SelectMany(method => method.GetParameters())
            .SelectMany(parameter => parameter.GetCustomAttributes<HttpTriggerAttribute>())
            .Select(attribute => attribute.Route)
            .Where(route => route is not null)
            .ToArray();

        routes.Should().NotBeEmpty();
        routes.Should().OnlyContain(route => route!.StartsWith("console/", StringComparison.Ordinal));
        routes.Should().NotContain(route => route!.StartsWith("admin/", StringComparison.Ordinal));
        routes.Should().BeEquivalentTo(
        [
            "console/ping",
            "console/users",
            "console/users/{userId}",
            "console/stats",
            "console/billing-support-requests",
            "console/billing-support-requests/{requestId}/resolve",
            "console/accounting/revenue.csv",
            "console/promo-codes",
            "console/promo-codes",
            "console/promo-codes/{promoCodeId}",
            "console/promo-codes/{promoCodeId}",
            "console/promo-codes/{promoCodeId}/disable",
            "console/promo-codes/{promoCodeId}/enable",
            "console/promo-codes/{promoCodeId}/archive",
            "console/promo-codes/{promoCodeId}/restore",
            "console/users/{userId}",
            "console/users/{userId}/credits",
            "console/users/{userId}/suspension",
            "console/users/{userId}/refund",
            "console/webhook-deliveries/{id}/retry",
        ]);
    }
}
