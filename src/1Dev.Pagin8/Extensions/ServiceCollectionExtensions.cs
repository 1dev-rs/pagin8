using _1Dev.Pagin8.Internal;
using _1Dev.Pagin8.Internal.Configuration;
using _1Dev.Pagin8.Internal.DateProcessor;
using _1Dev.Pagin8.Internal.Exceptions.Base;
using _1Dev.Pagin8.Internal.Exceptions.StatusCodes;
using _1Dev.Pagin8.Internal.Metadata;
using _1Dev.Pagin8.Internal.Metadata.Models;
using _1Dev.Pagin8.Internal.Tokenizer;
using _1Dev.Pagin8.Internal.Tokenizer.Contracts;
using _1Dev.Pagin8.Internal.Validators;
using _1Dev.Pagin8.Internal.Validators.Contracts;
using _1Dev.Pagin8.Internal.Visitors;
using Internal.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
                MaxSafeItemCount = 1_000_000,
            },
            MaxRelativeDateYears = 20
        };

        // Apply user overrides
        userConfig?.Invoke(config);

        ValidateConfig(config);

        // Configure InterpolatedSql parameter reuse (thread-safe - set once at startup)
        // When enabled, identical parameter values reuse the same @p0 instead of creating @p0, @p1, etc.
        // This is an optimization for queries with repeated values (e.g., keyset pagination)
        InterpolatedSql.SqlBuilders.InterpolatedSqlBuilderOptions.DefaultOptions.ReuseIdenticalParameters = true;

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
        services.AddTransient<IMetadataProvider, MetadataProvider>();
        services.AddTransient<IPagin8MetadataProvider, Pagin8MetadataProvider>();
        services.AddTransient<SqlQueryBuilder>();
        services.AddTransient<ISqlQueryBuilder>(sp =>
            new LoggingSqlQueryBuilder(
                sp.GetRequiredService<SqlQueryBuilder>(),
                sp.GetRequiredService<ILoggerFactory>()));
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
