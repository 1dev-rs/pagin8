using _1Dev.Pagin8.Extensions.Backend.Interfaces;
using System.Data;
using Microsoft.Data.SqlClient;

namespace _1Dev.Pagin8.Extensions.Backend.Implementations;

/// <summary>
/// SQL Server connection factory implementation.
/// Internal to keep the public API surface small; use the DI registration helpers to register providers.
/// </summary>
public class SqlServerConnectionFactory : IDbConnectionFactory, ISqlServerDbConnectionFactory
{
    private readonly string _connectionString;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqlServerConnectionFactory"/> class.
    /// </summary>
    /// <param name="connectionString">The connection string.</param>
    /// <exception cref="ArgumentNullException">Thrown when connection string is null or empty.</exception>
    public SqlServerConnectionFactory(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentNullException(nameof(connectionString));

        _connectionString = connectionString;
    }

    /// <summary>
    /// Creates a new SQL Server database connection.
    /// </summary>
    /// <returns>A new SQL Server connection instance.</returns>
    public IDbConnection Create()
    {
        return new SqlConnection(_connectionString);
    }
}
