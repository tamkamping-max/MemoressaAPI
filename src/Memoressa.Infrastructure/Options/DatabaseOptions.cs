namespace Memoressa.Infrastructure.Options;

public class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>
    /// <see cref="DatabaseTarget.Local"/> → MySQL (local dev).
    /// <see cref="DatabaseTarget.Rds"/> → PostgreSQL on Amazon RDS (release).
    /// </summary>
    public DatabaseTarget Target { get; set; } = DatabaseTarget.Local;
}

public enum DatabaseTarget
{
    Local,
    Rds
}
