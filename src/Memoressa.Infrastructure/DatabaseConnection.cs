using Memoressa.Infrastructure.Options;
using Microsoft.Extensions.Configuration;

namespace Memoressa.Infrastructure;

public static class DatabaseConnection
{
    public const string MySqlConnectionName = "MySql";
    public const string PostgreSqlConnectionName = "PostgreSql";

    public static DatabaseTarget ResolveTarget(IConfiguration configuration)
    {
        return configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>()?.Target
            ?? DatabaseTarget.Local;
    }

    public static string ResolveConnectionString(IConfiguration configuration)
    {
        var target = ResolveTarget(configuration);
        var name = target == DatabaseTarget.Rds ? PostgreSqlConnectionName : MySqlConnectionName;
        var connectionString = configuration.GetConnectionString(name);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"ConnectionStrings:{name} is required when Database:Target is '{target}'.");
        }

        return connectionString;
    }

    public static bool UsesPostgreSql(IConfiguration configuration) =>
        ResolveTarget(configuration) == DatabaseTarget.Rds;

    public static bool UsesMySql(IConfiguration configuration) =>
        ResolveTarget(configuration) == DatabaseTarget.Local;
}
