using _1Dev.Pagin8.Extensions.Backend.Models;

namespace _1Dev.Pagin8.Extensions.Backend.Interfaces;

/// <summary>
/// Base interface for repositories that support filtered queries.
/// </summary>
/// <typeparam name="TResponse">The response model type.</typeparam>
public interface IFilteredRepository<TResponse> where TResponse : class
{
    /// <summary>
    /// Gets filtered and paged data.
    /// </summary>
    /// <param name="query">The filtered data query parameters.</param>
    /// <returns>Paged results with data and total count.</returns>
    Task<PagedResults<TResponse>> GetFilteredAsync(FilteredDataQuery query);

    /// <summary>
    /// Gets the count of rows matching the filter.
    /// </summary>
    /// <param name="query">The filtered data query parameters.</param>
    /// <returns>The total count of matching rows.</returns>
    Task<int> GetFilteredCountAsync(FilteredDataQuery query);
}
