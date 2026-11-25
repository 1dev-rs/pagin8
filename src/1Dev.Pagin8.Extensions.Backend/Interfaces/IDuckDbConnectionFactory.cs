namespace _1Dev.Pagin8.Extensions.Backend.Interfaces;

/// <summary>
/// Marker interface for the DuckDB connection factory.
/// Used to differentiate from the primary database connection factory (e.g., PostgreSQL).
/// </summary>
public interface IDuckDbConnectionFactory : IDbConnectionFactory
{
    /// <summary>
    /// Optional default command timeout in seconds. Null means no timeout.
    /// DuckDB has no statement timeout of its own, so the query is interrupted when it runs out
    /// and a <see cref="TimeoutException"/> is thrown.
    /// </summary>
    int? CommandTimeout { get; }
}
