using _1Dev.Pagin8.Extensions.Backend.Models;
using Attributes;

namespace _1Dev.Pagin8.Extensions.Backend.Interfaces;

/// <summary>
/// Filter provider interface for executing Pagin8 queries.
/// </summary>
/// <remarks>
/// The overloads with a <see cref="CancellationToken"/> stop the query on the database when the
/// token is cancelled (PostgreSQL and SQL Server cancel the statement on the server, DuckDB
/// interrupts it) and throw <see cref="OperationCanceledException"/> for that token on every
/// provider. A command timeout still throws the provider's timeout exception. Their default
/// implementations, for providers written before they existed, ignore the token.
/// </remarks>
public interface IFilterProvider
{
    Task<PagedResults<TResponse>> GetAsync<TResponse>(string viewName, FilteredDataQuery query, int? commandTimeout = null)
        where TResponse : class;

    Task<int> GetCountAsync<TResponse>(string viewName, FilteredDataQuery query, int? commandTimeout = null)
        where TResponse : class;

    Task<IDictionary<string, decimal>> GetAggregatesAsync<T>(string viewName, FilteredDataQuery query, int? commandTimeout = null)
        where T : class;

    Task<PagedResults<TResponse>> GetAsync<TResponse>(string viewName, FilteredDataQuery query, CancellationToken cancellationToken, int? commandTimeout = null)
        where TResponse : class
        => GetAsync<TResponse>(viewName, query, commandTimeout);

    Task<int> GetCountAsync<TResponse>(string viewName, FilteredDataQuery query, CancellationToken cancellationToken, int? commandTimeout = null)
        where TResponse : class
        => GetCountAsync<TResponse>(viewName, query, commandTimeout);

    Task<IDictionary<string, decimal>> GetAggregatesAsync<T>(string viewName, FilteredDataQuery query, CancellationToken cancellationToken, int? commandTimeout = null)
        where T : class
        => GetAggregatesAsync<T>(viewName, query, commandTimeout);
}
