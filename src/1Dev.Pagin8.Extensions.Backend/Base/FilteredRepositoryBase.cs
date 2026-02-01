using _1Dev.Pagin8.Extensions.Backend.Interfaces;
using _1Dev.Pagin8.Extensions.Backend.Models;

namespace _1Dev.Pagin8.Extensions.Backend.Base;

/// <summary>
/// Base repository class for entities that support filtered queries.
/// Inherit from this class to get GetFilteredAsync support automatically.
/// </summary>
/// <typeparam name="TResponse">The response/DTO model type.</typeparam>
public abstract class FilteredRepositoryBase<TResponse> : IFilteredRepository<TResponse>
    where TResponse : class
{
    private readonly IFilterProvider _filterProvider;

    /// <summary>
    /// The database view or table name to query.
    /// </summary>
    protected abstract string ViewName { get; }

    /// <summary>
    /// Optional default filter to apply (e.g., "isDeleted=eq.false").
    /// </summary>
    protected virtual string? DefaultFilter => null;

    /// <summary>
    /// Initializes a new instance of the <see cref="FilteredRepositoryBase{TResponse}"/> class.
    /// </summary>
    /// <param name="filterProvider">The filter provider.</param>
    /// <exception cref="ArgumentNullException">Thrown when filterProvider is null.</exception>
    protected FilteredRepositoryBase(IFilterProvider filterProvider)
    {
        _filterProvider = filterProvider ?? throw new ArgumentNullException(nameof(filterProvider));
    }

    /// <summary>
    /// Gets filtered and paged data.
    /// </summary>
    /// <param name="query">The filtered data query parameters.</param>
    /// <returns>Paged results with data and total count.</returns>
    public virtual async Task<PagedResults<TResponse>> GetFilteredAsync(FilteredDataQuery query)
    {
        var effectiveQuery = ApplyDefaultFilter(query);
        return await _filterProvider.GetAsync<TResponse>(ViewName, effectiveQuery);
    }

    /// <summary>
    /// Gets the count of rows matching the filter.
    /// </summary>
    /// <param name="query">The filtered data query parameters.</param>
    /// <returns>The total count of matching rows.</returns>
    public virtual async Task<int> GetFilteredCountAsync(FilteredDataQuery query)
    {
        var effectiveQuery = ApplyDefaultFilter(query);
        return await _filterProvider.GetCountAsync<TResponse>(ViewName, effectiveQuery);
    }

    private FilteredDataQuery ApplyDefaultFilter(FilteredDataQuery query)
    {
        if (string.IsNullOrEmpty(DefaultFilter))
            return query;

        return query with { DefaultQuery = DefaultFilter };
    }
}
