using _1Dev.Pagin8.Internal.Configuration;
using Internal.Configuration;

namespace _1Dev.Pagin8.Test.SqlQueryBuilderTests.Internal;

public static class Pagin8TestBootstrap
{
    public static void Init()
    {
        Pagin8Runtime.Initialize(new ServiceConfiguration
        {
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