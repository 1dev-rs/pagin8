using _1Dev.Pagin8.Extensions;
using _1Dev.Pagin8.Internal.Configuration;
using _1Dev.Pagin8.Internal.DateProcessor;
using _1Dev.Pagin8.Internal.Tokenizer.Contracts;
using _1Dev.Pagin8.Internal.Validators.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace _1Dev.Pagin8.Test;

public class Pagin8DiSetupTests
{
    [Fact]
    public void AddPagin8_Should_Resolve_All_Core_Services()
    {
        // Arrange
        var services = new ServiceCollection();

        services.AddPagin8(options =>
        {
            options.DatabaseType = DatabaseType.PostgreSql;
            options.MaxNestingLevel = 5;
            options.PagingSettings = new PagingSettings
            {
                DefaultPerPage = 50,
                MaxItemsPerPage = 5000,
                MaxSafeItemCount = 1000000
            };
        });

        var provider = services.BuildServiceProvider();

        // Act & Assert
        provider.GetRequiredService<ITokenizer>();
        provider.GetRequiredService<IDateProcessor>();
        provider.GetRequiredService<ITokenizationService>();
        provider.GetRequiredService<IContextValidator>();
        provider.GetRequiredService<IPagin8MetadataProvider>();
        provider.GetRequiredService<IMetadataProvider>();
        provider.GetRequiredService<ISqlQueryBuilder>();
        provider.GetRequiredService<ISqlTokenVisitor>();

        // Example of generic
        var linqVisitor = typeof(ILinqTokenVisitor<>).MakeGenericType(typeof(object));
        var queryableProcessor = typeof(IQueryableTokenProcessor<>).MakeGenericType(typeof(object));

        Assert.NotNull(provider.GetRequiredService(linqVisitor));
        Assert.NotNull(provider.GetRequiredService(queryableProcessor));
    }
}