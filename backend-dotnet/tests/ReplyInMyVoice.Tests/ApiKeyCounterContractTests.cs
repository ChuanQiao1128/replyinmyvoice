using System.Reflection;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Domain.Entities;

namespace ReplyInMyVoice.Tests;

public sealed class ApiKeyCounterContractTests
{
    [Fact]
    public void CurrentPeriodUsageIsMarkedObsolete()
    {
        var property = typeof(ApiKey).GetProperty("CurrentPeriodUsage");

        property.Should().NotBeNull();
        var attribute = property!.GetCustomAttribute<ObsoleteAttribute>();
        attribute.Should().NotBeNull();
        attribute!.Message.Should().Contain("ApiKeyUsage");
        attribute.Message.Should().Contain("ApiKeyRateLimitWindow");
    }

    [Fact]
    public async Task CurrentPeriodUsageColumnStillMapped()
    {
        await using var fixture = await DbFixture.CreateAsync();
        await using var db = fixture.CreateContext();

        var property = db.Model
            .FindEntityType(typeof(ApiKey))!
            .FindProperty("CurrentPeriodUsage");

        property.Should().NotBeNull();
    }
}
