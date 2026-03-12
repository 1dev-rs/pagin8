namespace _1Dev.Pagin8.Extensions.Backend.Interfaces;

/// <summary>
/// Marker interface for SQL Server database connection factory.
/// Used to differentiate from the primary database connection factory (e.g., PostgreSQL).
/// </summary>
public interface ISqlServerDbConnectionFactory : IDbConnectionFactory
{
    /// <summary>
    /// Optional default command timeout in seconds for Dapper queries.
    /// Null means Dapper's built-in default (30 s) is used.
    /// </summary>
    int? CommandTimeout { get; }
}
