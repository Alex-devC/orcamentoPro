namespace OrcPro.Infrastructure.Persistence.Providers;

public static class DatabaseProviderFactory
{
    public static IDatabaseProvider Create(DatabaseProviderType providerType)
    {
        return providerType switch
        {
            DatabaseProviderType.Sqlite => new SQLiteProvider(),
            DatabaseProviderType.PostgreSql => new PostgreSqlProvider(),
            DatabaseProviderType.MySql => new MySqlProvider(),
            _ => throw new NotSupportedException(
                $"O provider de banco de dados '{providerType}' não é suportado.")
        };
    }
}
