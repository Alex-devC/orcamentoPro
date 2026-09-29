using Microsoft.EntityFrameworkCore;

namespace OrcPro.Infrastructure.Persistence.Providers;

public interface IDatabaseProviderConfigurator
{
    DatabaseProviderType ProviderType { get; }
    DbContextOptionsBuilder Configure(DbContextOptionsBuilder builder, string connectionString);
}

public class SqliteDatabaseProviderConfigurator : IDatabaseProviderConfigurator
{
    public DatabaseProviderType ProviderType => DatabaseProviderType.Sqlite;

    public DbContextOptionsBuilder Configure(DbContextOptionsBuilder builder, string connectionString)
    {
        return builder.UseSqlite(connectionString, options =>
        {
            options.MigrationsAssembly(typeof(SqliteDatabaseProviderConfigurator).Assembly.FullName);
        });
    }
}

public class PostgreSqlDatabaseProviderConfigurator : IDatabaseProviderConfigurator
{
    public DatabaseProviderType ProviderType => DatabaseProviderType.PostgreSql;

    public DbContextOptionsBuilder Configure(DbContextOptionsBuilder builder, string connectionString)
    {
        return builder.UseNpgsql(connectionString, options =>
        {
            options.MigrationsAssembly(typeof(PostgreSqlDatabaseProviderConfigurator).Assembly.FullName);
            options.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorCodesToAdd: null);
        });
    }
}

public class MySqlDatabaseProviderConfigurator : IDatabaseProviderConfigurator
{
    public DatabaseProviderType ProviderType => DatabaseProviderType.MySql;

    public DbContextOptionsBuilder Configure(DbContextOptionsBuilder builder, string connectionString)
    {
        var serverVersion = ServerVersion.AutoDetect(connectionString);
        return builder.UseMySql(connectionString, serverVersion, options =>
        {
            options.MigrationsAssembly(typeof(MySqlDatabaseProviderConfigurator).Assembly.FullName);
            options.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorNumbersToAdd: null);
        });
    }
}

public static class DatabaseProviderConfiguratorFactory
{
    public static IDatabaseProviderConfigurator ObterConfigurador(DatabaseProviderType providerType)
    {
        return providerType switch
        {
            DatabaseProviderType.Sqlite => new SqliteDatabaseProviderConfigurator(),
            DatabaseProviderType.PostgreSql => new PostgreSqlDatabaseProviderConfigurator(),
            DatabaseProviderType.MySql => new MySqlDatabaseProviderConfigurator(),
            _ => throw new NotSupportedException($"O provedor de banco de dados '{providerType}' não é suportado.")
        };
    }
}
