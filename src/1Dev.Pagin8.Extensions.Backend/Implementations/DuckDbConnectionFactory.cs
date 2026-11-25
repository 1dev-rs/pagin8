using System.Data;
using _1Dev.Pagin8.Extensions.Backend.Interfaces;
using DuckDB.NET.Data;

namespace _1Dev.Pagin8.Extensions.Backend.Implementations;

/// <summary>
/// DuckDB connection factory implementation.
/// </summary>
public class DuckDbConnectionFactory : IDuckDbConnectionFactory
{
    private readonly Func<DuckDBConnection> _createConnection;

    /// <summary>
    /// Opens a new DuckDB connection for every query.
    /// </summary>
    /// <param name="connectionString">The DuckDB connection string, e.g. <c>DataSource=:memory:</c> or <c>DataSource=archive.duckdb</c>.</param>
    /// <param name="commandTimeout">Optional default command timeout in seconds.</param>
    public DuckDbConnectionFactory(string connectionString, int? commandTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        _createConnection = () => new DuckDBConnection(connectionString);
        CommandTimeout = commandTimeout;
    }

    /// <summary>
    /// Uses connections the application creates, e.g. <c>DuckDBConnection.Duplicate()</c> of one open
    /// database, so its settings and caches are shared between queries.
    /// </summary>
    /// <param name="createConnection">Returns a new DuckDB connection, open or closed; the factory owns and disposes it.</param>
    /// <param name="commandTimeout">Optional default command timeout in seconds.</param>
    public DuckDbConnectionFactory(Func<DuckDBConnection> createConnection, int? commandTimeout = null)
    {
        _createConnection = createConnection ?? throw new ArgumentNullException(nameof(createConnection));
        CommandTimeout = commandTimeout;
    }

    /// <inheritdoc />
    public int? CommandTimeout { get; }

    /// <summary>
    /// Creates and opens a DuckDB connection that runs the SQL pagin8 builds.
    /// </summary>
    /// <returns>An open connection.</returns>
    public IDbConnection Create()
    {
        var connection = new DuckDbPagin8Connection(_createConnection());
        connection.Open();
        return connection;
    }
}
