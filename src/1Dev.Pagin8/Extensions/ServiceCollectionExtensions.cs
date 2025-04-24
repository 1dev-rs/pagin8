using _1Dev.Pagin8.Internal;
using _1Dev.Pagin8.Internal.Configuration;
using _1Dev.Pagin8.Internal.DateProcessor;
using _1Dev.Pagin8.Internal.Exceptions.Base;
using _1Dev.Pagin8.Internal.Exceptions.StatusCodes;
using _1Dev.Pagin8.Internal.Metadata;
using _1Dev.Pagin8.Internal.Tokenizer;
using _1Dev.Pagin8.Internal.Tokenizer.Contracts;
using _1Dev.Pagin8.Internal.Validators;
using _1Dev.Pagin8.Internal.Validators.Contracts;
using _1Dev.Pagin8.Internal.Visitors;
using Internal.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace _1Dev.Pagin8.Extensions;

public static class ServiceCollectionExtensions
{
    // ReSharper disable once UnusedMember.Global
    public static IServiceCollection AddPagin8(this IServiceCollection services, Action<ServiceConfiguration>? userConfig = null)
    {
        var config = new ServiceConfiguration
        {
            MaxNestingLevel = 5,
            PagingSettings = new PagingSettings
            {
                DefaultPerPage = 50,
                MaxItemsPerPage = 5000,
                MaxSafeItemCount = 1_000_000
            }
        };

        // Apply user overrides
        userConfig?.Invoke(config);

        ValidateConfig(config);

        // Store config for runtime/static access
        Pagin8Runtime.Initialize(config);

        services.AddSingleton(config);

        RegisterCoreServices(services, config);

        return services;
    }

    private static void ValidateConfig(ServiceConfiguration config)
    {
        if (config.MaxNestingLevel < 0)
            throw new Pagin8Exception(Pagin8StatusCode.Pagin8_PropertyValueMustBePositive.Code);
    }

    private static void RegisterCoreServices(IServiceCollection services, ServiceConfiguration config)
    {
        services.AddLogging();

        services.AddTransient<ITokenizer, Tokenizer>();
        services.AddTransient<IDateProcessor, DateProcessor>();
        services.AddTransient<ITokenizationService, TokenizationService>();
        services.AddTransient(typeof(ILinqTokenVisitor<>), typeof(LinqTokenVisitor<>));
        services.AddTransient<IContextValidator, TokenContextValidator>();
        services.AddTransient<IPagin8MetadataProvider, Pagin8MetadataProvider>();
        services.AddTransient<ISqlQueryBuilder, SqlQueryBuilder>();
        services.AddTransient(typeof(IQueryableTokenProcessor<>), typeof(QueryableTokenProcessor<>));

        RegisterDatabaseSpecificServices(services, config.DatabaseType);
    }

    private static void RegisterDatabaseSpecificServices(IServiceCollection services, DatabaseType dbType)
    {
        switch (dbType)
        {
            case DatabaseType.PostgreSql:
                services.AddTransient<ISqlTokenVisitor, NpgsqlTokenVisitor>();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(dbType), dbType, null);
        }
    }
}
