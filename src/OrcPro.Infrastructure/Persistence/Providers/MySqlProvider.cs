using Microsoft.EntityFrameworkCore;

namespace OrcPro.Infrastructure.Persistence.Providers;

public class MySqlProvider : IDatabaseProvider
{
    public DatabaseProviderType ProviderType => DatabaseProviderType.MySql;

    public DbContextOptionsBuilder Configure(DbContextOptionsBuilder builder, string connectionString)
    {
        var serverVersion = ServerVersion.AutoDetect(connectionString);
        return builder.UseMySql(connectionString, serverVersion, options =>
        {
            options.MigrationsAssembly(typeof(MySqlProvider).Assembly.FullName);
            options.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(5),
                errorNumbersToAdd: null);
        });
    }
}
