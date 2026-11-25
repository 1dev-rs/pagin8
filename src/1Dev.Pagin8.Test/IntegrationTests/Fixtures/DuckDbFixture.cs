using _1Dev.Pagin8.Test.IntegrationTests.Configuration;
using _1Dev.Pagin8.Test.IntegrationTests.Data;
using _1Dev.Pagin8.Test.IntegrationTests.Models;
using DuckDB.NET.Data;

namespace _1Dev.Pagin8.Test.IntegrationTests.Fixtures;

/// <summary>
/// In-memory DuckDB with the same seeded products as the container fixtures. Needs no Docker:
/// DuckDB runs in-process. Queries run on duplicates of one open connection, so they share the database.
/// </summary>
public class DuckDbFixture : IAsyncLifetime
{
    private readonly TestConfiguration _config = TestConfiguration.Instance;

    public DuckDBConnection Connection { get; } = new("DataSource=:memory:");

    public List<Product> Products { get; private set; } = [];

    public Task InitializeAsync()
    {
        Connection.Open();

        using (var command = Connection.CreateCommand())
        {
            command.CommandText = @"
                CREATE TABLE Products (
                    Id INTEGER PRIMARY KEY,
                    Name VARCHAR NOT NULL,
                    Category VARCHAR NOT NULL,
                    Status VARCHAR NOT NULL,
                    Price DECIMAL(18,2) NOT NULL,
                    Stock INTEGER NOT NULL,
                    Brand VARCHAR,
                    Description VARCHAR,
                    CreatedAt TIMESTAMP NOT NULL,
                    UpdatedAt TIMESTAMP,
                    Tags VARCHAR,
                    IsFeatured BOOLEAN NOT NULL,
                    Rating DOUBLE NOT NULL
                );";
            command.ExecuteNonQuery();
        }

        Products = TestDataSeeder.GenerateProducts(count: _config.TestSettings.DatasetSize, seed: _config.TestSettings.Seed);
        for (var i = 0; i < Products.Count; i++)
        {
            Products[i].Id = i + 1;
            // DuckDB keeps microseconds; trim .NET ticks so the seeded list compares equal to what is read back.
            Products[i].CreatedAt = TrimToMicroseconds(Products[i].CreatedAt);
            Products[i].UpdatedAt = Products[i].UpdatedAt is { } updated ? TrimToMicroseconds(updated) : null;
        }

        using (var appender = Connection.CreateAppender("Products"))
        {
            foreach (var p in Products)
            {
                var row = appender.CreateRow()
                    .AppendValue(p.Id)
                    .AppendValue(p.Name)
                    .AppendValue(p.Category)
                    .AppendValue(p.Status)
                    .AppendValue(p.Price)
                    .AppendValue(p.Stock);
                row = p.Brand is null ? row.AppendNullValue() : row.AppendValue(p.Brand);
                row = p.Description is null ? row.AppendNullValue() : row.AppendValue(p.Description);
                row = row.AppendValue(p.CreatedAt);
                row = p.UpdatedAt is null ? row.AppendNullValue() : row.AppendValue(p.UpdatedAt.Value);
                row = p.Tags is null ? row.AppendNullValue() : row.AppendValue(p.Tags);
                row.AppendValue(p.IsFeatured)
                    .AppendValue(p.Rating)
                    .EndRow();
            }
        }

        return Task.CompletedTask;
    }

    public DuckDBConnection CreateConnection() => Connection.Duplicate();

    public Task DisposeAsync()
    {
        Connection.Dispose();
        return Task.CompletedTask;
    }

    private static DateTime TrimToMicroseconds(DateTime value) =>
        new(value.Ticks - value.Ticks % 10, value.Kind);
}

[CollectionDefinition("DuckDb")]
public class DuckDbCollection : ICollectionFixture<DuckDbFixture>
{
}
