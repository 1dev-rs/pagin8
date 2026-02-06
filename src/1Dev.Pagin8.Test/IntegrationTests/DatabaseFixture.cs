using Microsoft.Data.SqlClient;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Xunit;

namespace _1Dev.Pagin8.Test.IntegrationTests;

[Table("Archive")]
public class ArchiveRecord
{
    [Key]
    public int Id { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime RecordDate { get; set; }
    public decimal Amount { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
}

/// <summary>
/// Database fixture for integration tests
/// Implements IAsyncLifetime to setup/teardown database connection
/// </summary>
public class DatabaseFixture : IAsyncLifetime
{
    public string ConnectionString { get; } = @"Server=(localdb)\MSSQLLocalDB;Database=Pagin8Test;Integrated Security=true;";
    public SqlConnection? Connection { get; private set; }

    public async Task InitializeAsync()
    {
        Connection = new SqlConnection(ConnectionString);
        await Connection.OpenAsync();

        // Verify database exists and has data
        var count = await Dapper.SqlMapper.ExecuteScalarAsync<int>(
            Connection, 
            "SELECT COUNT(*) FROM Archive");

        if (count == 0)
        {
            throw new InvalidOperationException(
                "Database has no records. Please run SetupDatabase.sql or SetupDatabase_300k.sql first.\n" +
                "Command: sqlcmd -S \"(localdb)\\MSSQLLocalDB\" -i src/1Dev.Pagin8.Test/DatabaseSetup/SetupDatabase.sql");
        }
    }

    public async Task DisposeAsync()
    {
        if (Connection != null)
        {
            await Connection.DisposeAsync();
        }
    }
}

/// <summary>
/// Collection definition for sharing database fixture across test classes
/// </summary>
[CollectionDefinition("Database Collection")]
public class DatabaseCollection : ICollectionFixture<DatabaseFixture>
{
    // This class has no code, and is never created. Its purpose is to be the place
    // to apply [CollectionDefinition] and all the ICollectionFixture<> interfaces.
}
