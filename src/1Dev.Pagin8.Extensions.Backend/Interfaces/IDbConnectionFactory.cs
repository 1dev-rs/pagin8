using System.Data;

namespace _1Dev.Pagin8.Extensions.Backend.Interfaces;

/// <summary>
/// Database connection factory interface.
/// </summary>
public interface IDbConnectionFactory
{
    /// <summary>
    /// Creates a new database connection.
    /// </summary>
    /// <returns>An open database connection.</returns>
    IDbConnection Create();
}
