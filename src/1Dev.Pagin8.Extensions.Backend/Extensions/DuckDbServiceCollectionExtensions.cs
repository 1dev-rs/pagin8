using _1Dev.Pagin8.Extensions.Backend.Implementations;
using _1Dev.Pagin8.Extensions.Backend.Interfaces;
using _1Dev.Pagin8.Internal;
using _1Dev.Pagin8.Internal.Configuration;
using _1Dev.Pagin8.Internal.DateProcessor;
using _1Dev.Pagin8.Internal.Tokenizer;
using _1Dev.Pagin8.Internal.Tokenizer.Contracts;
using _1Dev.Pagin8.Internal.Validators.Contracts;
using _1Dev.Pagin8.Internal.Visitors;
using DuckDB.NET.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace _1Dev.Pagin8.Extensions.Backend.Extensions;

/// <summary>
/// Registers the DuckDB filter provider (<see cref="IDuckDbFilterProvider"/>) next to the primary one.
/// </summary>
/// <remarks>
/// DuckDB runs the PostgreSQL SQL pagin8 emits, so it requires <c>DatabaseType.PostgreSql</c> (the
/// default) in <c>AddPagin8</c>: the operator maps are chosen by that setting process-wide, and SQL
/// Server's LIKE would be case-sensitive on DuckDB. Call order: 1) AddPagin8(), 2) AddPagin8BackendDuckDb().
/// The application references DuckDB's native library itself, e.g. the DuckDB.NET.Data.Full package.
/// </remarks>
public static class DuckDbServiceCollectionExtensions
{
    /// <summary>
    /// Adds the DuckDB filter provider, opening a new connection for every query.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">The DuckDB connection string, e.g. <c>DataSource=:memory:</c>.</param>
    /// <param name="commandTimeout">Optional default command timeout in seconds.</param>
    public static IServiceCollection AddPagin8BackendDuckDb(
        this IServiceCollection services,
        string connectionString,
        int? commandTimeout = null)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentNullException(nameof(connectionString));

        return services.AddPagin8BackendDuckDb(new DuckDbConnectionFactory(connectionString, commandTimeout));
    }

    /// <summary>
    /// Adds the DuckDB filter provider over connections the application creates, e.g.
    /// <c>DuckDBConnection.Duplicate()</c> of one open database.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="createConnection">Returns a new DuckDB connection; the provider owns and disposes it.</param>
    /// <param name="commandTimeout">Optional default command timeout in seconds.</param>
    public static IServiceCollection AddPagin8BackendDuckDb(
        this IServiceCollection services,
        Func<DuckDBConnection> createConnection,
        int? commandTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(createConnection);

        return services.AddPagin8BackendDuckDb(new DuckDbConnectionFactory(createConnection, commandTimeout));
    }

    /// <summary>
    /// Adds the DuckDB filter provider with the given connection factory.
    /// </summary>
    public static IServiceCollection AddPagin8BackendDuckDb(
        this IServiceCollection services,
        IDuckDbConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);

        return services.AddPagin8BackendDuckDb(_ => connectionFactory, configure: null);
    }

    /// <summary>
    /// Adds the DuckDB filter provider over connections created from the application's services,
    /// e.g. <c>sp =&gt; sp.GetRequiredService&lt;Store&gt;().Duplicate()</c> of a singleton that holds the
    /// open database.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="createConnection">Returns a new DuckDB connection; the provider owns and disposes it.</param>
    /// <param name="configure">Command timeout and the validator for DuckDB queries.</param>
    public static IServiceCollection AddPagin8BackendDuckDb(
        this IServiceCollection services,
        Func<IServiceProvider, DuckDBConnection> createConnection,
        Action<DuckDbProviderOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(createConnection);

        var options = new DuckDbProviderOptions();
        configure?.Invoke(options);

        return services.AddPagin8BackendDuckDb(
            sp => new DuckDbConnectionFactory(() => createConnection(sp), options.CommandTimeout),
            options);
    }

    /// <summary>
    /// Adds the DuckDB filter provider with a connection factory created from the application's services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionFactory">Creates the connection factory, once.</param>
    /// <param name="configure">The validator for DuckDB queries (the factory sets the command timeout).</param>
    public static IServiceCollection AddPagin8BackendDuckDb(
        this IServiceCollection services,
        Func<IServiceProvider, IDuckDbConnectionFactory> connectionFactory,
        Action<DuckDbProviderOptions>? configure)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);

        var options = new DuckDbProviderOptions();
        configure?.Invoke(options);

        return services.AddPagin8BackendDuckDb(connectionFactory, options);
    }

    private static IServiceCollection AddPagin8BackendDuckDb(
        this IServiceCollection services,
        Func<IServiceProvider, IDuckDbConnectionFactory> connectionFactory,
        DuckDbProviderOptions options)
    {
        EnsurePagin8CoreRegisteredForPostgreSql(services);

        services.AddSingleton(connectionFactory);

        services.AddScoped<IDuckDbSqlQueryBuilder>(sp =>
        {
            var visitor = new DuckDbTokenVisitor(
                sp.GetRequiredService<IPagin8MetadataProvider>(),
                sp.GetRequiredService<IDateProcessor>());
            var tokenization = options.ContextValidator is null
                ? sp.GetRequiredService<ITokenizationService>()
                : new TokenizationService(
                    sp.GetRequiredService<ITokenizer>(),
                    options.ContextValidator(sp),
                    sp.GetRequiredService<IPagin8MetadataProvider>());
            var inner = new DuckDbSqlQueryBuilder(tokenization, visitor);
            return new LoggingDuckDbSqlQueryBuilder(inner, sp.GetRequiredService<ILoggerFactory>());
        });

        services.AddScoped<IDuckDbFilterProvider>(sp => new DuckDbFilterProvider(
            sp.GetRequiredService<IDuckDbConnectionFactory>(),
            sp.GetRequiredService<IDuckDbSqlQueryBuilder>()));

        return services;
    }

    private static void EnsurePagin8CoreRegisteredForPostgreSql(IServiceCollection services)
    {
        var required = new[]
        {
            typeof(ITokenizationService),
            typeof(IPagin8MetadataProvider),
            typeof(IDateProcessor),
            typeof(ServiceConfiguration)
        };

        var missing = required.Where(t => services.All(sd => sd.ServiceType != t)).ToArray();
        if (missing.Length > 0)
        {
            var names = string.Join(", ", missing.Select(t => t.Name));
            throw new InvalidOperationException($"Pagin8 core services are not registered: {names}. Call services.AddPagin8(...) before AddPagin8BackendDuckDb().");
        }

        var config = services
            .Where(sd => sd.ServiceType == typeof(ServiceConfiguration))
            .Select(sd => sd.ImplementationInstance)
            .OfType<ServiceConfiguration>()
            .LastOrDefault();

        if (config is not null && config.DatabaseType != DatabaseType.PostgreSql)
            throw new InvalidOperationException($"AddPagin8BackendDuckDb() requires DatabaseType.PostgreSql in AddPagin8(), but it is {config.DatabaseType}: DuckDB runs the PostgreSQL SQL pagin8 emits.");
    }
}
