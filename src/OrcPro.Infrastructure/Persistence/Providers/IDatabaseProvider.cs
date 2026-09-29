using Microsoft.EntityFrameworkCore;

namespace OrcPro.Infrastructure.Persistence.Providers;

public interface IDatabaseProvider
{
    DatabaseProviderType ProviderType { get; }

    DbContextOptionsBuilder Configure(DbContextOptionsBuilder builder, string connectionString);
}
