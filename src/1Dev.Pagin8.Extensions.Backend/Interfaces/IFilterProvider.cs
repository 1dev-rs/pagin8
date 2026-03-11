using _1Dev.Pagin8.Extensions.Backend.Models;
using Attributes;

namespace _1Dev.Pagin8.Extensions.Backend.Interfaces;

/// <summary>
/// Filter provider interface for executing Pagin8 queries.
/// </summary>
public interface IFilterProvider
{
    /// <summary>
    /// Executes a filtered query and returns paged results.
    /// </summary>
    /// <typeparam name="TResponse">The response model type.</typeparam>
    /// <param name="viewName">The table or view name to query.</param>
    /// <param name="query">The filtered data query parameters.</param>
    /// <param name="commandTimeout">Optional SQL command timeout in seconds. Null uses the Dapper default (30 s).</param>
    /// <returns>Paged results with data and total count.</returns>
    Task<PagedResults<TResponse>> GetAsync<TResponse>(string viewName, FilteredDataQuery query, int? commandTimeout = null)
        where TResponse : class;

    /// <summary>
    /// Gets the count of rows matching the filter.
    /// </summary>
    /// <typeparam name="TResponse">The response model type.</typeparam>
    /// <param name="viewName">The table or view name to query.</param>
    /// <param name="query">The filtered data query parameters.</param>
    /// <param name="commandTimeout">Optional SQL command timeout in seconds. Null uses the Dapper default (30 s).</param>
    /// <returns>The total count of matching rows.</returns>
    Task<int> GetCountAsync<TResponse>(string viewName, FilteredDataQuery query, int? commandTimeout = null)
        where TResponse : class;

    /// <summary>
    /// Gets aggregate values (SUM, COUNT, etc.) for properties decorated with <see cref="AggregateAttribute"/>.
    /// </summary>
    /// <typeparam name="T">The response model type whose properties carry <see cref="AggregateAttribute"/>.</typeparam>
    /// <param name="viewName">The table or view name to query.</param>
    /// <param name="query">The filtered data query parameters.</param>
    /// <param name="commandTimeout">Optional SQL command timeout in seconds. Null uses the Dapper default (30 s).</param>
    /// <returns>A dictionary mapping alias names to their computed aggregate values.</returns>
    Task<IDictionary<string, decimal>> GetAggregatesAsync<T>(string viewName, FilteredDataQuery query, int? commandTimeout = null)
        where T : class;

}
