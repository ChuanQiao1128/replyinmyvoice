using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ReplyInMyVoice.Infrastructure.Data;

public sealed class AppDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var provider = Environment.GetEnvironmentVariable("RIMV_EF_PROVIDER");
        if (string.Equals(provider, "sqlite", StringComparison.OrdinalIgnoreCase))
        {
            var sqliteConnectionString = Environment.GetEnvironmentVariable("RIMV_EF_SQLITE_CONNECTION") ??
                "Data Source=replyinmyvoice-design-time.db";
            var sqliteOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(sqliteConnectionString)
                .Options;

            return new AppDbContext(sqliteOptions);
        }

        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection") ??
            Environment.GetEnvironmentVariable("ConnectionStrings:DefaultConnection") ??
            Environment.GetEnvironmentVariable("DATABASE_URL") ??
            "Server=localhost,1433;Database=ReplyInMyVoiceDesignTime;User ID=sa;Password=ReplyInMyVoice_DevOnly_123!;TrustServerCertificate=True";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new AppDbContext(options);
    }
}
