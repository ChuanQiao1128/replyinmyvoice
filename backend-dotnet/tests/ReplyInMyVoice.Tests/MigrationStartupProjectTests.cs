using FluentAssertions;
using ReplyInMyVoice.Infrastructure.Data;

namespace ReplyInMyVoice.Tests;

public sealed class MigrationStartupProjectTests
{
    [Fact]
    public void Design_time_factory_uses_sql_server_provider()
    {
        using var context = new AppDbContextDesignTimeFactory().CreateDbContext(Array.Empty<string>());

        context.Should().NotBeNull();
        context.Database.ProviderName.Should().Be("Microsoft.EntityFrameworkCore.SqlServer");
    }
}
