namespace _1Dev.Pagin8.Extensions.Backend.Models;

/// <summary>
/// Query parameters for filtered data requests.
/// </summary>
public record FilteredDataQuery
{
    /// <summary>
    /// The Pagin8 query string (e.g., "status=eq.active&sort(name.asc)").
    /// </summary>
    public string QueryString { get; init; } = string.Empty;

    /// <summary>
    /// Default query to apply if no filters provided.
    /// </summary>
    public string DefaultQuery { get; init; } = string.Empty;

    /// <summary>
    /// Whether to ignore the limit clause.
    /// </summary>
    public bool IgnoreLimit { get; init; }

    /// <summary>
    /// Creates a new FilteredDataQuery from a query string.
    /// </summary>
    /// <param name="queryString">The query string from HTTP request.</param>
    /// <param name="ignoreLimit">Whether to ignore the limit clause.</param>
    /// <returns>A new FilteredDataQuery instance.</returns>
    public static FilteredDataQuery Create(string? queryString, bool ignoreLimit = false)
        => new()
        {
            QueryString = queryString?.TrimStart('?') ?? string.Empty,
            IgnoreLimit = ignoreLimit
        };

    /// <summary>
    /// Creates a new FilteredDataQuery with a default filter.
    /// </summary>
    /// <param name="queryString">The query string from HTTP request.</param>
    /// <param name="defaultQuery">Default filter to apply.</param>
    /// <param name="ignoreLimit">Whether to ignore the limit clause.</param>
    /// <returns>A new FilteredDataQuery instance.</returns>
    public static FilteredDataQuery Create(string? queryString, string defaultQuery, bool ignoreLimit = false)
        => new()
        {
            QueryString = queryString?.TrimStart('?') ?? string.Empty,
            DefaultQuery = defaultQuery,
            IgnoreLimit = ignoreLimit
        };
}
