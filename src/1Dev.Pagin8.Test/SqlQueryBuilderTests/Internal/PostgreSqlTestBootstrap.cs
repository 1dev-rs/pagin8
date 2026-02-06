using _1Dev.Pagin8.Internal.Configuration;
using Internal.Configuration;

namespace _1Dev.Pagin8.Test.SqlQueryBuilderTests.Internal;

public static class PostgreSqlTestBootstrap
{
    public static void Init()
    {
        // Explicitly set PostgreSQL for these tests
        // This ensures correct behavior even when SQL Server integration tests run first
        Pagin8Runtime.Initialize(new ServiceConfiguration
        {
            MaxNestingLevel = 5,
            PagingSettings = new PagingSettings
            {
                DefaultPerPage = 50,
                MaxItemsPerPage = 5000,
                MaxSafeItemCount = 1_000_000
            },
            DatabaseType = DatabaseType.PostgreSql  // Explicitly set PostgreSQL
        });
    }
}