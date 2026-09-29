using Microsoft.EntityFrameworkCore;

namespace OrcPro.Infrastructure.Persistence.Providers;

public class PostgreSqlProvider : IDatabaseProvider
{
    public DatabaseProviderType ProviderType => DatabaseProviderType.PostgreSql;

    public DbContextOptionsBuilder Configure(DbContextOptionsBuilder builder, string connectionString)
    {
        return builder.UseNpgsql(connectionString, options =>
        {
            options.MigrationsAssembly(typeof(PostgreSqlProvider).Assembly.FullName);
            options.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(5),
                errorCodesToAdd: null);
        });
    }
}
