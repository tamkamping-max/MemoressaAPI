using System.Net.Sockets;
using Memoressa.Infrastructure.Data;
using Memoressa.Infrastructure.Options;
using Microsoft.EntityFrameworkCore;
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
        var connectionName = target == DatabaseTarget.Rds
            ? DatabaseConnection.PostgreSqlConnectionName
            : DatabaseConnection.MySqlConnectionName;

        if (target == DatabaseTarget.Local)
        {
            logger.LogInformation(
                "Database:Target=Local (MySQL, connection {ConnectionName}). Apply schema with ./database/mysql/apply.sh if tables are missing.",
                connectionName);
        }
        else
        {
            logger.LogInformation(
                "Database:Target=Rds (PostgreSQL, connection {ConnectionName}). Apply schema with ./database/apply.sh if tables are missing.",
                connectionName);
        }
    }

    public static async Task VerifyConnectivityAsync(IHost host, CancellationToken cancellationToken = default)
    {
        var configuration = host.Services.GetRequiredService<IConfiguration>();
        var logger = host.Services
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("DatabaseBootstrap");
        var target = DatabaseConnection.ResolveTarget(configuration);

        try
        {
            using var scope = host.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MemoressaDbContext>();
            if (await db.Database.CanConnectAsync(cancellationToken))
            {
                logger.LogInformation("Database connectivity check succeeded.");
                return;
            }

            logger.LogError(
                "Database connectivity check failed (CanConnectAsync returned false). {Hint}",
                GetHint(target));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Database connectivity check failed. {Hint}", GetHint(target));
        }
    }

    public static string GetHint(DatabaseTarget target) =>
        target == DatabaseTarget.Local
            ? "Local dev: run `docker compose up -d`, apply `./database/mysql/apply.sh`, and start the API with ASPNETCORE_ENVIRONMENT=Development (Database:Target=Local)."
            : "Release: set ConnectionStrings:PostgreSql to a reachable RDS instance and Database:Target=Rds.";

    public static bool IsConnectionFailure(Exception exception)
    {
        for (var ex = exception; ex is not null; ex = ex.InnerException)
        {
            if (ex is SocketException or TimeoutException)
            {
                return true;
            }

            var typeName = ex.GetType().FullName ?? string.Empty;
            if (typeName.Contains("Npgsql", StringComparison.Ordinal)
                || typeName.Contains("MySql", StringComparison.Ordinal)
                || typeName.Contains("DbException", StringComparison.Ordinal))
            {
                return true;
            }

            if (ex.Message.Contains("transient failure", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("Unable to connect to any of the specified MySQL hosts", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("Failed to connect", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static string GetConnectionFailureMessage(IConfiguration configuration) =>
        GetHint(DatabaseConnection.ResolveTarget(configuration));
}
