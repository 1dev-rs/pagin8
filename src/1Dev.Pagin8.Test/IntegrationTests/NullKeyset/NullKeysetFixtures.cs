using _1Dev.Pagin8.Test.IntegrationTests.Configuration;
using Dapper;
using DuckDB.NET.Data;
using Microsoft.Data.SqlClient;
using Npgsql;
using Testcontainers.MsSql;
using Testcontainers.PostgreSql;
using Xunit;

namespace _1Dev.Pagin8.Test.IntegrationTests.NullKeyset;

/// <summary>
/// The NULL keyset tests share global pagin8 configuration (the database type) and so run alone,
/// after every parallel collection.
/// </summary>
[CollectionDefinition("NullKeyset", DisableParallelization = true)]
public class NullKeysetCollection;

/// <summary>
/// PostgreSQL for the NULL keyset tests: a fresh database on the server named by
/// PAGIN8_TEST_POSTGRES_CONNECTION (an admin connection string; the database is dropped at the
/// end), otherwise a Testcontainers PostgreSQL, which CI runs.
/// </summary>
public class NullKeysetPostgreSqlFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;
    private string? _adminConnectionString;
    private readonly string _database = $"pagin8_nulls_{Guid.NewGuid():N}";

    public string ConnectionString { get; private set; } = "";

    public async Task InitializeAsync()
    {
        _adminConnectionString = Environment.GetEnvironmentVariable("PAGIN8_TEST_POSTGRES_CONNECTION");
        if (string.IsNullOrWhiteSpace(_adminConnectionString))
        {
            var settings = TestConfiguration.Instance.TestSettings.Databases.PostgreSql;
            _container = new PostgreSqlBuilder().WithImage(settings.Image).Build();
            await _container.StartAsync();
            _adminConnectionString = _container.GetConnectionString();
        }

        await using (var admin = new NpgsqlConnection(_adminConnectionString))
            await admin.ExecuteAsync($"CREATE DATABASE {_database}");

        ConnectionString = new NpgsqlConnectionStringBuilder(_adminConnectionString) { Database = _database, Pooling = false }.ConnectionString;

        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.ExecuteAsync(NullRowData.PostgreSqlCreateTable);
        await connection.ExecuteAsync(NullRowData.Insert, NullRowData.Rows);
    }

    public async Task DisposeAsync()
    {
        if (_adminConnectionString != null)
        {
            await using var admin = new NpgsqlConnection(_adminConnectionString);
            await admin.ExecuteAsync($"DROP DATABASE IF EXISTS {_database} WITH (FORCE)");
        }

        if (_container != null)
            await _container.DisposeAsync();
    }
}

/// <summary>
/// SQL Server for the NULL keyset tests: a fresh database on the server named by
/// PAGIN8_TEST_SQLSERVER_CONNECTION, otherwise a Testcontainers SQL Server, which CI runs.
/// </summary>
public class NullKeysetSqlServerFixture : IAsyncLifetime
{
    private MsSqlContainer? _container;
    private string? _adminConnectionString;
    private readonly string _database = $"pagin8_nulls_{Guid.NewGuid():N}";

    public string ConnectionString { get; private set; } = "";

    public async Task InitializeAsync()
    {
        _adminConnectionString = Environment.GetEnvironmentVariable("PAGIN8_TEST_SQLSERVER_CONNECTION");
        if (string.IsNullOrWhiteSpace(_adminConnectionString))
        {
            var settings = TestConfiguration.Instance.TestSettings.Databases.SqlServer;
            _container = new MsSqlBuilder().WithImage(settings.Image).WithPassword(settings.Password).Build();
            await _container.StartAsync();
            _adminConnectionString = _container.GetConnectionString();
        }

        await using (var admin = new SqlConnection(_adminConnectionString))
            await admin.ExecuteAsync($"CREATE DATABASE {_database}");

        ConnectionString = new SqlConnectionStringBuilder(_adminConnectionString) { InitialCatalog = _database, Pooling = false }.ConnectionString;

        await using var connection = new SqlConnection(ConnectionString);
        await connection.ExecuteAsync(NullRowData.SqlServerCreateTable);
        await connection.ExecuteAsync(NullRowData.Insert, NullRowData.Rows);
    }

    public async Task DisposeAsync()
    {
        if (_adminConnectionString != null)
        {
            await using var admin = new SqlConnection(_adminConnectionString);
            await admin.ExecuteAsync($"IF DB_ID('{_database}') IS NOT NULL BEGIN ALTER DATABASE {_database} SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE {_database}; END");
        }

        if (_container != null)
            await _container.DisposeAsync();
    }
}

/// <summary>DuckDB for the NULL keyset tests, in memory; queries run on duplicates of one connection.</summary>
public class NullKeysetDuckDbFixture : IAsyncLifetime
{
    public DuckDBConnection Connection { get; } = new("DataSource=:memory:");

    public DuckDBConnection CreateConnection() => Connection.Duplicate();

    public async Task InitializeAsync()
    {
        await Connection.OpenAsync();
        await Connection.ExecuteAsync(NullRowData.DuckDbCreateTable);

        foreach (var row in NullRowData.Rows)
        {
            await using var command = Connection.CreateCommand();
            command.CommandText = $"INSERT INTO {NullRowData.Table} VALUES ($1, $2, $3, $4, $5, $6, $7, $8)";
            foreach (var value in new object?[] { row.Id, row.Label, row.Note, row.Quantity, row.Amount, row.Shipped, row.Flag, row.Code })
                command.Parameters.Add(new DuckDBParameter(value ?? DBNull.Value));
            await command.ExecuteNonQueryAsync();
        }
    }

    public Task DisposeAsync()
    {
        Connection.Dispose();
        return Task.CompletedTask;
    }
}
