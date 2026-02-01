using System.Data;
using Npgsql;
using _1Dev.Pagin8.Extensions.Backend.Interfaces;

namespace _1Dev.Pagin8.Extensions.Backend.Implementations;

/// <summary>
/// PostgreSQL connection factory implementation.
/// </summary>
public class NpgsqlConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    /// <summary>
    /// Initializes a new instance of the <see cref="NpgsqlConnectionFactory"/> class.
    /// </summary>
    /// <param name="connectionString">The PostgreSQL connection string.</param>
    /// <exception cref="ArgumentNullException">Thrown when connection string is null.</exception>
    public NpgsqlConnectionFactory(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    /// <summary>
    /// Creates and opens a new PostgreSQL connection.
    /// </summary>
    /// <returns>An open NpgsqlConnection instance.</returns>
    public IDbConnection Create()
    {
        var connection = new NpgsqlConnection(_connectionString);
        connection.Open();
        return connection;
    }
}
