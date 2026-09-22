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
    /// Local MySQL: create schema from EF model on first dev run.
    /// RDS PostgreSQL: schema must be applied via database/migrations/*.sql (never auto-created here).
    /// </summary>
    public static void EnsureDevelopmentSchema(IHost host)
    {
        if (!host.Services.GetRequiredService<IHostEnvironment>().IsDevelopment())
        {
            return;
        }

        var configuration = host.Services.GetRequiredService<IConfiguration>();
        if (DatabaseConnection.UsesPostgreSql(configuration))
        {
            return;
        }

        using var scope = host.Services.CreateScope();
        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("DatabaseBootstrap");
        var db = scope.ServiceProvider.GetRequiredService<MemoressaDbContext>();

        logger.LogInformation(
            "Database:Target=Local — ensuring MySQL schema via EF (RDS release uses database/migrations SQL instead)");
        db.Database.EnsureCreated();
    }
}
