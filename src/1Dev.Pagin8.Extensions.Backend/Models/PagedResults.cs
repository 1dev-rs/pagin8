using System.Text.Json.Serialization;
using _1Dev.Pagin8.Internal;

namespace _1Dev.Pagin8.Extensions.Backend.Models;

/// <summary>
/// Generic paged results model returned from filtered queries.
/// </summary>
/// <typeparam name="T">The type of data items.</typeparam>
public record PagedResults<T>
{
    /// <summary>
    /// The data items for the current page.
    /// </summary>
    public IEnumerable<T> Data { get; init; } = [];

    /// <summary>
    /// Total number of rows matching the filter (requires count.true in query).
    /// </summary>
    public int TotalRows { get; init; }

    /// <summary>
    /// Internal Pagin8 metadata. Not serialized to JSON.
    /// </summary>
    [JsonIgnore]
    public Meta? Meta { get; set; }
}
