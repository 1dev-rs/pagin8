using Dapper;
using Npgsql;
using Testcontainers.PostgreSql;
using _1Dev.Pagin8.Test.IntegrationTests.Configuration;
using _1Dev.Pagin8.Test.IntegrationTests.Data;
using _1Dev.Pagin8.Test.IntegrationTests.Models;
using Xunit;

namespace _1Dev.Pagin8.Test.IntegrationTests.Fixtures;

/// <summary>
/// PostgreSQL Testcontainer fixture
/// Automatically spins up PostgreSQL in Docker, creates schema, and seeds data
/// </summary>
/// <remarks>
/// Configuration priority:
/// 1. Environment variables (PAGIN8_TEST_DATASET_SIZE, PAGIN8_TEST_SEED, PAGIN8_TEST_PRESET)
/// 2. test-config.json file
/// 3. Default values (5000 records, seed 42)
/// </remarks>
public class PostgreSqlContainerFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;
    private readonly TestConfiguration _config;

    public string ConnectionString => _container?.GetConnectionString() ?? string.Empty;
    public NpgsqlConnection? Connection { get; private set; }
    public int DatasetSize => _config.TestSettings.DatasetSize;
    public int Seed => _config.TestSettings.Seed;
    // Performance metrics collector removed; integration tests only

    public PostgreSqlContainerFixture()
    {
        // Load configuration from test-config.json with environment variable overrides
        _config = TestConfiguration.Instance;
    }

    public async Task InitializeAsync()
    {
        var settings = _config.TestSettings.Databases.PostgreSql;
        
        // Create and start PostgreSQL container
        _container = new PostgreSqlBuilder()
            .WithImage(settings.Image)
            .WithDatabase(settings.Database)
            .WithUsername(settings.Username)
            .WithPassword(settings.Password)
            .WithCleanUp(_config.TestSettings.Docker.CleanupOnExit)
            .Build();

        Console.WriteLine("Starting PostgreSQL container...");
        await _container.StartAsync();
        Console.WriteLine($"PostgreSQL container started: {_container.GetConnectionString()}");

        // Connect to the database
        Connection = new NpgsqlConnection(ConnectionString);
        await Connection.OpenAsync();

        // Create table
        Console.WriteLine("Creating Products table...");
        await Connection.ExecuteAsync(TestDataSeeder.GetPostgreSqlCreateTableScript());

        // Seed data
        var startTime = DateTime.UtcNow;
        var products = TestDataSeeder.GenerateProducts(count: DatasetSize, seed: Seed);
        Console.WriteLine($"Seeding {products.Count:N0} products (seed: {Seed})...");
        
        await Connection.ExecuteAsync(
            TestDataSeeder.GetPostgreSqlInsertScript(),
            products
        );

        var count = await Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Products");
        var elapsed = DateTime.UtcNow - startTime;
        Console.WriteLine($"? PostgreSQL ready with {count:N0} products (seeded in {elapsed.TotalSeconds:F2}s)");
        
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

        if (_container != null)
        {
            Console.WriteLine("Stopping PostgreSQL container...");
            await _container.DisposeAsync();
        }
    }
}

/// <summary>
/// Collection definition for PostgreSQL Testcontainer
/// </summary>
[CollectionDefinition("PostgreSql Testcontainer")]
public class PostgreSqlContainerCollection : ICollectionFixture<PostgreSqlContainerFixture>
{
}
