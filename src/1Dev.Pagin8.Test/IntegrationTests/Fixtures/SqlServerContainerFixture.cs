using Dapper;
using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;
using _1Dev.Pagin8.Test.IntegrationTests.Configuration;
using _1Dev.Pagin8.Test.IntegrationTests.Data;
using _1Dev.Pagin8.Test.IntegrationTests.Models;
using Xunit;

namespace _1Dev.Pagin8.Test.IntegrationTests.Fixtures;

/// <summary>
/// SQL Server Testcontainer fixture
/// Automatically spins up SQL Server in Docker, creates schema, and seeds data
/// </summary>
/// <remarks>
/// Configuration priority:
/// 1. Environment variables (PAGIN8_TEST_DATASET_SIZE, PAGIN8_TEST_SEED, PAGIN8_TEST_PRESET)
/// 2. test-config.json file
/// 3. Default values (5000 records, seed 42)
/// </remarks>
public class SqlServerContainerFixture : IAsyncLifetime
{
    private MsSqlContainer? _container;
    private readonly TestConfiguration _config;

    private string? _adminConnectionString;
    private string? _externalConnectionString;
    private readonly string _database = $"pagin8_it_{Guid.NewGuid():N}";

    public string ConnectionString => _externalConnectionString ?? _container?.GetConnectionString() ?? string.Empty;
    public SqlConnection? Connection { get; private set; }
    public int DatasetSize => _config.TestSettings.DatasetSize;
    public int Seed => _config.TestSettings.Seed;
    public string? ContainerStartFailure { get; private set; }
    // Performance metrics collector removed; integration-only fixture

    public SqlServerContainerFixture()
    {
        // Load configuration from test-config.json with environment variable overrides
        _config = TestConfiguration.Instance;
        // integration-only: no performance metrics
    }

    public async Task InitializeAsync()
    {
        var settings = _config.TestSettings.Databases.SqlServer;

        if (!await UseServerFromEnvironmentAsync() && !await StartContainerAsync(settings))
            return;

        await SeedAsync(settings);
    }

    // PAGIN8_TEST_SQLSERVER_CONNECTION runs the tests on a server of your own instead of Docker,
    // in a fresh database that is dropped at the end.
    private async Task<bool> UseServerFromEnvironmentAsync()
    {
        _adminConnectionString = Environment.GetEnvironmentVariable("PAGIN8_TEST_SQLSERVER_CONNECTION");
        if (string.IsNullOrWhiteSpace(_adminConnectionString))
            return false;

        await using (var admin = new SqlConnection(_adminConnectionString))
            await admin.ExecuteAsync($"CREATE DATABASE {_database}");

        _externalConnectionString = new SqlConnectionStringBuilder(_adminConnectionString) { InitialCatalog = _database, Pooling = false }.ConnectionString;
        Console.WriteLine($"SQL Server from PAGIN8_TEST_SQLSERVER_CONNECTION, database {_database}");
        return true;
    }

    private async Task<bool> StartContainerAsync(SqlServerSettings settings)
    {
        // Create and start SQL Server container
        _container = new MsSqlBuilder()
            .WithImage(settings.Image)
            .WithPassword(settings.Password)
            .WithCleanUp(_config.TestSettings.Docker.CleanupOnExit)
            .Build();

        Console.WriteLine("Starting SQL Server container...");
        try
        {
            await _container.StartAsync();
        }
        catch (Exception ex)
        {
            ContainerStartFailure =
                $"SQL Server container failed to start. " +
                $"Make sure Docker is running and the image is available locally.\n" +
                $"  Pull command : docker pull {settings.Image}\n" +
                $"  Image setting: test-config.json \u2192 testConfiguration.databases.sqlServer.image\n" +
                $"  Inner error  : {ex.Message}";
            Console.WriteLine($"[ERROR] {ContainerStartFailure}");
            return false;
        }
        Console.WriteLine($"SQL Server container started: {_container.GetConnectionString()}");
        return true;
    }

    private async Task SeedAsync(SqlServerSettings settings)
    {
        // Connect to the database
        Connection = new SqlConnection(ConnectionString);
        await Connection.OpenAsync();

        // Create table
        Console.WriteLine("Creating Products table...");
        await Connection.ExecuteAsync(TestDataSeeder.GetSqlServerCreateTableScript());

        // Seed data
        var startTime = DateTime.UtcNow;
        var products = TestDataSeeder.GenerateProducts(count: DatasetSize, seed: Seed);
        Console.WriteLine($"Seeding {products.Count:N0} products (seed: {Seed})...");
        
        await Connection.ExecuteAsync(
            TestDataSeeder.GetSqlServerInsertScript(),
            products
        );

        var count = await Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Products");
        var elapsed = DateTime.UtcNow - startTime;
        Console.WriteLine($"? SQL Server ready with {count:N0} products (seeded in {elapsed.TotalSeconds:F2}s)");
        
        if (_config.TestSettings.EnablePerformanceMetrics)
        {
            Console.WriteLine($"?? Configuration: {DatasetSize:N0} records, seed={Seed}, image={settings.Image}");
        }
    }

    public async Task DisposeAsync()
    {
        // No performance metrics to print; integration-only cleanup
        
        if (Connection != null)
        {
            await Connection.DisposeAsync();
        }

        if (_externalConnectionString != null)
        {
            await using var admin = new SqlConnection(_adminConnectionString);
            await admin.ExecuteAsync($"ALTER DATABASE {_database} SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE {_database};");
        }

        if (_container != null)
        {
            Console.WriteLine("Stopping SQL Server container...");
            await _container.DisposeAsync();
        }
    }
}

/// <summary>
/// Collection definition for SQL Server Testcontainer
/// </summary>
[CollectionDefinition("SqlServer Testcontainer")]
public class SqlServerContainerCollection : ICollectionFixture<SqlServerContainerFixture>
{
}
