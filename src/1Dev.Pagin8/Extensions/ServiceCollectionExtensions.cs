using _1Dev.Pagin8;
using _1Dev.Pagin8.Internal;
using _1Dev.Pagin8.Internal.Configuration;
using _1Dev.Pagin8.Internal.DateProcessor;
using _1Dev.Pagin8.Internal.Metadata;
using _1Dev.Pagin8.Internal.Tokenizer;
using _1Dev.Pagin8.Internal.Tokenizer.Contracts;
using _1Dev.Pagin8.Internal.Utils;
using _1Dev.Pagin8.Internal.Validators;
using _1Dev.Pagin8.Internal.Validators.Contracts;
using _1Dev.Pagin8.Internal.Visitors;
using Microsoft.Extensions.DependencyInjection;
using ConfigurationProvider = _1Dev.Pagin8.Internal.Configuration.ConfigurationProvider;

namespace _1Dev.Pagin8.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPagin8(this IServiceCollection services, Action<ServiceConfiguration> serviceConfig)
    {
        var configuration = new ServiceConfiguration();
        serviceConfig(configuration);

        Guard.AgainstNull(configuration);
        Guard.AgainstNull(configuration.DatabaseType);

        services.AddLogging();

        services.AddTransient(typeof(ILinqTokenVisitor<>), typeof(LinqTokenVisitor<>));
        services.AddTransient<IPagin8MetadataProvider, Pagin8MetadataProvider>();
        services.AddTransient(typeof(IContextValidator), typeof(TokenContextValidator));
        services.AddTransient<ITokenizer, Tokenizer>();
        services.AddTransient<IDateProcessor, DateProcessor>();
        services.AddTransient(typeof(ITokenizationService), typeof(TokenizationService));
        CreateVisitor(services, configuration.DatabaseType);
        services.AddTransient(typeof(ISqlQueryBuilder), typeof(SqlQueryBuilder));

        services.AddTransient(typeof(IQueryableTokenProcessor<>), typeof(QueryableTokenProcessor<>));

        ConfigurationProvider.LoadSettings(configuration);

        return services;
    }

    private static void CreateVisitor(IServiceCollection services, DatabaseType databaseType)
    {
        switch (databaseType)
        {
            case DatabaseType.PostgreSql:
                services.AddTransient(typeof(ISqlTokenVisitor), typeof(NpgsqlTokenVisitor));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(databaseType), databaseType, null);
        }
    }
}

