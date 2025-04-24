using _1Dev.Pagin8.Internal.Configuration;
using Internal.Configuration;

namespace _1Dev.Pagin8.Test;

public class Pagin8TestBase
{
    static Pagin8TestBase()
    {
        Pagin8Runtime.Initialize(new ServiceConfiguration
        {
            DatabaseType = DatabaseType.PostgreSql,
            MaxNestingLevel = 5,
            PagingSettings = new PagingSettings
            {
                DefaultPerPage = 50,
                MaxItemsPerPage = 5000,
                MaxSafeItemCount = 1_000_000
            }
        });
    }
}