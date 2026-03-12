using _1Dev.Pagin8.Extensions.Backend.Models;
using Attributes;

namespace _1Dev.Pagin8.Extensions.Backend.Interfaces;

/// <summary>
/// Filter provider interface for executing Pagin8 queries.
/// </summary>
public interface IFilterProvider
{
    Task<PagedResults<TResponse>> GetAsync<TResponse>(string viewName, FilteredDataQuery query, int? commandTimeout = null)
        where TResponse : class;

    Task<int> GetCountAsync<TResponse>(string viewName, FilteredDataQuery query, int? commandTimeout = null)
        where TResponse : class;

    Task<IDictionary<string, decimal>> GetAggregatesAsync<T>(string viewName, FilteredDataQuery query, int? commandTimeout = null)
        where T : class;

}
