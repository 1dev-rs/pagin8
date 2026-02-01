using Microsoft.Extensions.DependencyInjection;
using _1Dev.Pagin8.Extensions.Backend.Implementations;
using _1Dev.Pagin8.Extensions.Backend.Interfaces;

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

        // Connection factory
        services.AddSingleton<IDbConnectionFactory>(
            new NpgsqlConnectionFactory(connectionString));

        // Filter provider
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
        services.AddSingleton<IDbConnectionFactory, TConnectionFactory>();
        services.AddScoped<IFilterProvider, FilterProvider>();

        return services;
    }
}
