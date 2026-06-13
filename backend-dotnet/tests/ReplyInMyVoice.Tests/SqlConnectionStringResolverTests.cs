using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using ReplyInMyVoice.Infrastructure.Data;

namespace ReplyInMyVoice.Tests;

public sealed class SqlConnectionStringResolverTests
{
    [Fact]
    public void Resolve_returns_original_when_flag_off()
    {
        const string configuredConnectionString =
            "Server=localhost;Database=ReplyInMyVoiceTest;User Id=test;Password=test;TrustServerCertificate=True";
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = configuredConnectionString,
        });

        SqlConnectionStringResolver.Resolve(configuration)
            .Should()
            .Be(configuredConnectionString);
    }

    [Fact]
    public void Resolve_strips_credentials_and_sets_active_directory_default()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["USE_MANAGED_IDENTITY"] = "true",
            ["ConnectionStrings:DefaultConnection"] =
                "Server=localhost;Database=ReplyInMyVoiceTest;User Id=test-user;Password=test-password;TrustServerCertificate=True",
        });

        var resolved = SqlConnectionStringResolver.Resolve(configuration);
        var builder = new SqlConnectionStringBuilder(resolved);

        builder.Authentication.Should().Be(SqlAuthenticationMethod.ActiveDirectoryDefault);
        resolved.Should().Contain("Authentication=ActiveDirectoryDefault");
        resolved.Should().NotContain("test-user");
        resolved.Should().NotContain("test-password");
    }

    [Fact]
    public void Resolve_preserves_existing_authentication_method()
    {
        var baseBuilder = new SqlConnectionStringBuilder
        {
            DataSource = "localhost",
            InitialCatalog = "ReplyInMyVoiceTest",
            Authentication = SqlAuthenticationMethod.ActiveDirectoryIntegrated,
            TrustServerCertificate = true,
        };
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["USE_MANAGED_IDENTITY"] = "true",
            ["ConnectionStrings:DefaultConnection"] = baseBuilder.ConnectionString,
        });

        var resolved = SqlConnectionStringResolver.Resolve(configuration);
        var builder = new SqlConnectionStringBuilder(resolved);

        builder.Authentication.Should().Be(SqlAuthenticationMethod.ActiveDirectoryIntegrated);
    }

    [Fact]
    public void Resolve_builds_from_azure_sql_settings()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["USE_MANAGED_IDENTITY"] = "true",
            ["AZURE_SQL_SERVER"] = "rimv-sql.database.windows.net",
            ["AZURE_SQL_DATABASE"] = "rimv-db",
        });

        var resolved = SqlConnectionStringResolver.Resolve(configuration);
        var builder = new SqlConnectionStringBuilder(resolved);

        builder.DataSource.Should().Be("rimv-sql.database.windows.net");
        builder.InitialCatalog.Should().Be("rimv-db");
        builder.Authentication.Should().Be(SqlAuthenticationMethod.ActiveDirectoryDefault);
        builder.Encrypt.ToString().Should().Be("True");
        builder.ConnectTimeout.Should().Be(30);
    }

    [Fact]
    public void Resolve_returns_null_when_nothing_configured()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["USE_MANAGED_IDENTITY"] = "true",
        });

        SqlConnectionStringResolver.Resolve(configuration).Should().BeNull();
    }

    [Fact]
    public void Resolve_throws_without_echoing_value_on_malformed_connection_string()
    {
        const string malformedConnectionString =
            "Server=localhost;Password=should-not-appear;NotARealSqlClientKeyword=value";
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["USE_MANAGED_IDENTITY"] = "true",
            ["ConnectionStrings:DefaultConnection"] = malformedConnectionString,
        });

        var exception = Assert.Throws<InvalidOperationException>(() =>
            SqlConnectionStringResolver.Resolve(configuration));

        exception.Message.Should().Be(
            "ConnectionStrings:DefaultConnection could not be parsed for managed identity mode.");
        exception.Message.Should().NotContain(malformedConnectionString);
        exception.Message.Should().NotContain("should-not-appear");
    }

    private static IConfiguration BuildConfiguration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
}
