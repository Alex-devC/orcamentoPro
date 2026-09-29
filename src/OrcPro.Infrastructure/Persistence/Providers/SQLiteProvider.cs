using Microsoft.EntityFrameworkCore;

namespace OrcPro.Infrastructure.Persistence.Providers;

public class SQLiteProvider : IDatabaseProvider
{
    public DatabaseProviderType ProviderType => DatabaseProviderType.Sqlite;

    public DbContextOptionsBuilder Configure(DbContextOptionsBuilder builder, string connectionString)
    {
        return builder.UseSqlite(connectionString, options =>
        {
            options.MigrationsAssembly(typeof(SQLiteProvider).Assembly.FullName);
        });
    }
}
