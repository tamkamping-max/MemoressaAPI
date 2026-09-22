using Memoressa.Infrastructure.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Memoressa.Infrastructure;

public static class DatabaseBootstrap
{
    /// <summary>
    /// Local MySQL and RDS PostgreSQL schemas are applied via SQL scripts
    /// (<c>database/mysql/*.sql</c> or <c>database/migrations/*.sql</c>), not at API startup.
    /// </summary>
    public static void LogDatabaseTarget(IHost host)
    {
        var configuration = host.Services.GetRequiredService<IConfiguration>();
        var logger = host.Services
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("DatabaseBootstrap");

        var target = DatabaseConnection.ResolveTarget(configuration);
        if (target == DatabaseTarget.Local)
        {
            logger.LogInformation(
                "Database:Target=Local (MySQL). Apply schema with ./database/mysql/apply.sh if tables are missing.");
        }
        else
        {
            logger.LogInformation(
                "Database:Target=Rds (PostgreSQL). Apply schema with ./database/apply.sh if tables are missing.");
        }
    }
}
