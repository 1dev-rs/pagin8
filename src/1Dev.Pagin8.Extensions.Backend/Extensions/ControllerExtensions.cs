using Microsoft.AspNetCore.Http;
using _1Dev.Pagin8.Extensions.Backend.Models;

namespace _1Dev.Pagin8.Extensions.Backend.Extensions;

/// <summary>
/// Extension methods for ASP.NET Core controllers.
/// </summary>
public static class ControllerExtensions
{
    /// <summary>
    /// Creates a FilteredDataQuery from the current HTTP request query string.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <param name="ignoreLimit">Whether to ignore the limit clause.</param>
    /// <returns>A new FilteredDataQuery.</returns>
    public static FilteredDataQuery ToFilteredDataQuery(
        this HttpContext context,
        bool ignoreLimit = false)
    {
        return FilteredDataQuery.Create(context.Request.QueryString.Value, ignoreLimit);
    }

    /// <summary>
    /// Creates a FilteredDataQuery with a default filter from the current HTTP request.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <param name="defaultQuery">Default filter to apply.</param>
    /// <param name="ignoreLimit">Whether to ignore the limit clause.</param>
    /// <returns>A new FilteredDataQuery.</returns>
    public static FilteredDataQuery ToFilteredDataQuery(
        this HttpContext context,
        string defaultQuery,
        bool ignoreLimit = false)
    {
        return FilteredDataQuery.Create(
            context.Request.QueryString.Value,
            defaultQuery,
            ignoreLimit);
    }
}
