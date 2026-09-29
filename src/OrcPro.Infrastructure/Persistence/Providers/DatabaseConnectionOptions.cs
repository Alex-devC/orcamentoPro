namespace OrcPro.Infrastructure.Persistence.Providers;

public enum DatabaseProviderType
{
    Sqlite = 1,
    PostgreSql = 2,
    MySql = 3
}

public class DatabaseConnectionOptions
{
    public const string SectionName = "DatabaseConnection";

    public DatabaseProviderType Provider { get; set; } = DatabaseProviderType.Sqlite;
    public string ConnectionString { get; set; } = "Data Source=OrcPro.db";

    public static DatabaseConnectionOptions DefaultSqlite(string? customDbPath = null)
    {
        var path = string.IsNullOrWhiteSpace(customDbPath) ? "OrcPro.db" : customDbPath;
        return new DatabaseConnectionOptions
        {
            Provider = DatabaseProviderType.Sqlite,
            ConnectionString = $"Data Source={path}"
        };
    }
}
