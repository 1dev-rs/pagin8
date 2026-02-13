using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using _1Dev.Pagin8.Internal.Configuration;
using _1Dev.Pagin8;
using _1Dev.Pagin8.Extensions.Backend.Implementations;
using _1Dev.Pagin8.Extensions.Backend.Interfaces;
using _1Dev.Pagin8.Internal;
using _1Dev.Pagin8.Internal.DateProcessor;
using _1Dev.Pagin8.Internal.Tokenizer.Contracts;
using _1Dev.Pagin8.Internal.Visitors;

namespace _1Dev.Pagin8.Extensions.Backend.Extensions;

/// <summary>
/// Extension methods for service registration.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Pagin8 backend services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">The PostgreSQL connection string.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when connectionString is null.</exception>
    public static IServiceCollection AddPagin8Backend(
        this IServiceCollection services,
        string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentNullException(nameof(connectionString));

        // Ensure core Pagin8 services are registered
        EnsurePagin8CoreRegistered(services);

        // Connection factory
        services.AddSingleton<IDbConnectionFactory>(
            new NpgsqlConnectionFactory(connectionString));

        // Filter provider
        services.AddScoped<IFilterProvider, FilterProvider>();

        return services;
    }

    /// <summary>
    /// Adds Pagin8 backend services with a factory function.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionFactoryFunc">Factory function to create the connection factory.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddPagin8Backend(
        this IServiceCollection services,
        Func<IDbConnectionFactory> connectionFactoryFunc)
    {
        if (connectionFactoryFunc == null)
            throw new ArgumentNullException(nameof(connectionFactoryFunc));

        // Ensure core Pagin8 services are registered
        EnsurePagin8CoreRegistered(services);

        services.AddSingleton<IDbConnectionFactory>(sp => connectionFactoryFunc());
        services.AddScoped<IFilterProvider, FilterProvider>();

        return services;
    }

    /// <summary>
    /// Adds Pagin8 backend services with custom connection factory.
    /// </summary>
    /// <typeparam name="TConnectionFactory">The connection factory type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddPagin8Backend<TConnectionFactory>(
        this IServiceCollection services)
        where TConnectionFactory : class, IDbConnectionFactory
    {
        // Ensure core Pagin8 services are registered
        EnsurePagin8CoreRegistered(services);
        services.AddSingleton<IDbConnectionFactory, TConnectionFactory>();
        services.AddScoped<IFilterProvider, FilterProvider>();

        return services;
    }

    /// <summary>
    /// Adds Pagin8 backend services for SQL Server with a named provider.
    /// Use this to add SQL Server support alongside PostgreSQL.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">The SQL Server connection string.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// IMPORTANT: This method requires that AddPagin8() has been called first, 
    /// as it depends on core Pagin8 services (ITokenizationService, IPagin8MetadataProvider, IDateProcessor).
    /// Call order: 1) AddPagin8(), 2) AddPagin8Backend(), 3) AddPagin8BackendSqlServer()
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when connectionString is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when core Pagin8 services are not registered.</exception>
    public static IServiceCollection AddPagin8BackendSqlServer(
        this IServiceCollection services,
        string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentNullException(nameof(connectionString));

        // Register SQL Server connection factory
        services.AddSingleton<ISqlServerDbConnectionFactory>(
            new SqlServerConnectionFactory(connectionString));

        // Register remaining SQL Server runtime services (query builder + filter provider)
        RegisterSqlServerRuntimeServices(services);

        return services;
    }

    /// <summary>
    /// Adds Pagin8 backend services for SQL Server using a factory function to create the connection factory.
    /// </summary>
    public static IServiceCollection AddPagin8BackendSqlServer(
        this IServiceCollection services,
        Func<ISqlServerDbConnectionFactory> connectionFactoryFunc)
    {
        if (connectionFactoryFunc == null)
            throw new ArgumentNullException(nameof(connectionFactoryFunc));

        services.AddSingleton<ISqlServerDbConnectionFactory>(sp => connectionFactoryFunc());

        RegisterSqlServerRuntimeServices(services);

        return services;
    }

    /// <summary>
    /// Adds Pagin8 backend services for SQL Server with custom connection factory type.
    /// </summary>
    public static IServiceCollection AddPagin8BackendSqlServer<TConnectionFactory>(
        this IServiceCollection services)
        where TConnectionFactory : class, ISqlServerDbConnectionFactory
    {
        services.AddSingleton<ISqlServerDbConnectionFactory, TConnectionFactory>();

        RegisterSqlServerRuntimeServices(services);

        return services;
    }

    private static void RegisterSqlServerRuntimeServices(IServiceCollection services)
    {
        // Ensure core Pagin8 services are registered (AddPagin8 must be called first)
        EnsurePagin8CoreRegistered(services);

        // Register SQL Server-specific query builder with SqlServerTokenVisitor
        // This is separate from the main ISqlQueryBuilder which uses PostgreSQL visitor
        services.AddScoped<ISqlServerSqlQueryBuilder>(sp =>
        {
            // Note: These services must be registered by calling AddPagin8() first
            var tokenizationService = sp.GetRequiredService<ITokenizationService>();
            var metadata = sp.GetRequiredService<IPagin8MetadataProvider>();
            var dateProcessor = sp.GetRequiredService<IDateProcessor>();

            // Create SQL Server token visitor
            var sqlServerVisitor = new SqlServerTokenVisitor(metadata, dateProcessor);

            // Create query builder with SQL Server visitor
            return new SqlServerSqlQueryBuilder(tokenizationService, sqlServerVisitor);
        });

        // Register SQL Server filter provider
        services.AddScoped<ISqlServerFilterProvider, SqlServerFilterProvider>();
    }

    private static void EnsurePagin8CoreRegistered(IServiceCollection services)
    {
        var required = new[]
        {
            typeof(ITokenizationService),
            typeof(IPagin8MetadataProvider),
            typeof(IDateProcessor),
            typeof(ISqlQueryBuilder),
            typeof(ServiceConfiguration)
        };

        var missing = required.Where(t => !services.Any(sd => sd.ServiceType == t)).ToArray();
        if (missing.Length > 0)
        {
            var names = string.Join(", ", missing.Select(t => t.Name));
            throw new InvalidOperationException($"Pagin8 core services are not registered: {names}. Call services.AddPagin8(...) before AddPagin8BackendSqlServer().");
        }
    }
}
