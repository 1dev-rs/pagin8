namespace _1Dev.Pagin8.Extensions.Backend.Interfaces;

/// <summary>
/// Marker interface for SQL Server database connection factory.
/// Used to differentiate from the primary database connection factory (e.g., PostgreSQL).
/// </summary>
public interface ISqlServerDbConnectionFactory : IDbConnectionFactory
{
}
