using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using ReplyInMyVoice.Infrastructure.Configuration;

namespace ReplyInMyVoice.Infrastructure.Data;

public static class SqlConnectionStringResolver
{
    public static string? Resolve(IConfiguration configuration)
    {
        var baseConnectionString = configuration.GetConnectionString("DefaultConnection")
            ?? configuration["DATABASE_URL"];
        if (!ManagedIdentityConfiguration.IsEnabled(configuration))
        {
            return baseConnectionString;
        }

        if (!string.IsNullOrWhiteSpace(baseConnectionString))
        {
            SqlConnectionStringBuilder builder;
            try
            {
                builder = new SqlConnectionStringBuilder(baseConnectionString);
            }
            catch (Exception ex) when (ex is ArgumentException or FormatException)
            {
                throw new InvalidOperationException(
                    "ConnectionStrings:DefaultConnection could not be parsed for managed identity mode.");
            }

            builder.Remove("Password");
            builder.Remove("User ID");
            if (builder.Authentication == SqlAuthenticationMethod.NotSpecified)
            {
                builder.Authentication = SqlAuthenticationMethod.ActiveDirectoryDefault;
            }

            return builder.ConnectionString;
        }

        var azureSqlServer = configuration["AZURE_SQL_SERVER"];
        var azureSqlDatabase = configuration["AZURE_SQL_DATABASE"];
        if (string.IsNullOrWhiteSpace(azureSqlServer) ||
            string.IsNullOrWhiteSpace(azureSqlDatabase))
        {
            return null;
        }

        return new SqlConnectionStringBuilder
        {
            DataSource = azureSqlServer,
            InitialCatalog = azureSqlDatabase,
            Authentication = SqlAuthenticationMethod.ActiveDirectoryDefault,
            Encrypt = true,
            ConnectTimeout = 30,
        }.ConnectionString;
    }
}
