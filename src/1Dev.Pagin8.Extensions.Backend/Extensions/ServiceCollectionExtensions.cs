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

        // Register the un-named factory instance as before
        services.AddSingleton<ISqlServerDbConnectionFactory>(sp => connectionFactoryFunc());

        // Also ensure the named provider mapping contains the default provider
        services.AddSingleton<ISqlServerDbConnectionFactoryProvider>(sp =>
        {
            var provider = new SqlServerDbConnectionFactoryProvider();
            var existing = sp.GetService<ISqlServerDbConnectionFactory>();
            if (existing != null)
            {
                provider.Add(SqlServerConstants.DefaultProviderName, existing);
            }
            return provider;
        });

        RegisterSqlServerRuntimeServices(services);

        return services;
    }

    /// <summary>
    /// Adds Pagin8 backend services for SQL Server with a specific named provider.
    /// </summary>
    public static IServiceCollection AddPagin8BackendSqlServer(
        this IServiceCollection services,
        string name,
        string connectionString)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentNullException(nameof(name));
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentNullException(nameof(connectionString));

        // Ensure core Pagin8 services are registered
        EnsurePagin8CoreRegistered(services);

        // Accumulate named providers at registration time (not resolve time) to avoid
        // circular dependency and multiple-descriptor problems when called more than once.
        var existingDescriptor = services.FirstOrDefault(
            s => s.ServiceType == typeof(ISqlServerDbConnectionFactoryProvider)
              && s.ImplementationInstance is SqlServerDbConnectionFactoryProvider);

        if (existingDescriptor?.ImplementationInstance is SqlServerDbConnectionFactoryProvider existingProvider)
        {
            // Reuse the already-registered instance and add the new named factory to it.
            existingProvider.Add(name, new SqlServerConnectionFactory(connectionString));
        }
        else
        {
            // First call: remove any factory-based descriptors and register a concrete instance.
            var stale = services.Where(s => s.ServiceType == typeof(ISqlServerDbConnectionFactoryProvider)).ToList();
            foreach (var d in stale) services.Remove(d);

            var provider = new SqlServerDbConnectionFactoryProvider();
            provider.Add(name, new SqlServerConnectionFactory(connectionString));
            services.AddSingleton<ISqlServerDbConnectionFactoryProvider>(provider);
        }

        // Register SQL Server runtime services (query builder + filter provider factory)
        RegisterSqlServerRuntimeServices(services);

        return services;
    }

    /// <summary>
    /// Adds Pagin8 backend services for SQL Server with a factory function and a provider name.
    /// </summary>
    public static IServiceCollection AddPagin8BackendSqlServer(
        this IServiceCollection services,
        string name,
        Func<ISqlServerDbConnectionFactory> connectionFactoryFunc)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentNullException(nameof(name));
        if (connectionFactoryFunc == null)
            throw new ArgumentNullException(nameof(connectionFactoryFunc));

        EnsurePagin8CoreRegistered(services);

        services.AddSingleton<ISqlServerDbConnectionFactoryProvider>(sp =>
        {
            var existingProvider = sp.GetService<ISqlServerDbConnectionFactoryProvider>() ?? new SqlServerDbConnectionFactoryProvider();
            existingProvider.Add(name, connectionFactoryFunc());
            return existingProvider;
        });

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
        services.AddScoped<ISqlServerSqlQueryBuilder>(sp =>
        {
            var tokenizationService = sp.GetRequiredService<ITokenizationService>();
            var metadata = sp.GetRequiredService<IPagin8MetadataProvider>();
            var dateProcessor = sp.GetRequiredService<IDateProcessor>();

            var sqlServerVisitor = new SqlServerTokenVisitor(metadata, dateProcessor);
            return new SqlServerSqlQueryBuilder(tokenizationService, sqlServerVisitor);
        });

        // Register a factory to create SQL Server filter providers for a given named connection
        services.AddScoped<ISqlServerFilterProviderFactory, SqlServerFilterProviderFactory>();

        // For backward compatibility, if code requests ISqlServerFilterProvider directly,
        // resolve the default named provider.
        services.AddScoped<ISqlServerFilterProvider>(sp =>
        {
            var factory = sp.GetRequiredService<ISqlServerFilterProviderFactory>();
            return factory.Create(SqlServerConstants.DefaultProviderName);
        });
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
