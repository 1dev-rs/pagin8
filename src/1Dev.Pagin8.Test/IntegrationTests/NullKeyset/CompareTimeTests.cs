using System.ComponentModel.DataAnnotations;
using _1Dev.Pagin8.Extensions;
using _1Dev.Pagin8.Extensions.Backend.Extensions;
using _1Dev.Pagin8.Extensions.Backend.Interfaces;
using _1Dev.Pagin8.Extensions.Backend.Models;
using _1Dev.Pagin8.Internal.Attributes;
using _1Dev.Pagin8.Internal.DateProcessor;
using _1Dev.Pagin8.Internal.Metadata;
using _1Dev.Pagin8.Internal.Metadata.Models;
using _1Dev.Pagin8.Internal.Tokenizer;
using _1Dev.Pagin8.Internal.Visitors;
using Dapper;
using DuckDB.NET.Data;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace _1Dev.Pagin8.Test.IntegrationTests.NullKeyset;

public class TimeRow
{
    [Key]
    public int Id { get; set; }

    [CompareTime]
    public DateTime? Moment { get; set; }

    // The same moments without [CompareTime] (and never NULL): compared by date, as before.
    public DateTime Plain { get; set; }

    [CompareTime]
    public DateOnly Day { get; set; }
}

public static class TimeRowData
{
    public const string Table = "time_rows";

    public static IReadOnlyList<TimeRow> Rows { get; } =
    [
        Row(1, new DateTime(2026, 11, 25, 0, 0, 0)),
        Row(2, new DateTime(2026, 11, 25, 12, 29, 59, 999)),
        Row(3, new DateTime(2026, 11, 25, 12, 30, 0)),
        Row(4, new DateTime(2026, 11, 25, 12, 30, 15, 500)),
        Row(5, new DateTime(2026, 11, 25, 12, 30, 59, 999)),
        Row(6, new DateTime(2026, 11, 25, 12, 31, 0)),
        Row(7, new DateTime(2026, 11, 25, 23, 59, 59, 999)),
        Row(8, new DateTime(2026, 11, 26, 0, 0, 0)),
        Row(9, null),
    ];

    private static TimeRow Row(int id, DateTime? moment)
    {
        var plain = moment ?? new DateTime(2026, 11, 24, 8, 0, 0);
        return new TimeRow { Id = id, Moment = moment, Plain = plain, Day = DateOnly.FromDateTime(plain) };
    }

    /// <summary>A filter on a [CompareTime] property and the ids it selects, the same on every provider.</summary>
    public static TheoryData<string, int[]> Cases => new()
    {
        // A value is the interval of its precision.
        { "moment=eq.2026-11-25", [1, 2, 3, 4, 5, 6, 7] },
        { "moment=eq.2026-11-25T12:30", [3, 4, 5] },
        { "moment=eq.2026-11-25T12:30:15", [4] },
        { "moment=eq.2026-11-25T12:30:15.500", [4] },
        { "moment=gte.2026-11-25T12:30", [3, 4, 5, 6, 7, 8] },
        { "moment=gt.2026-11-25T12:30", [6, 7, 8] },
        { "moment=lt.2026-11-25T12:30", [1, 2] },
        { "moment=lte.2026-11-25T12:30", [1, 2, 3, 4, 5] },
        { "moment=lte.2026-11-25T12:30:00", [1, 2, 3] },
        // A day: the rows DATE() selected without the attribute.
        { "moment=lte.2026-11-25", [1, 2, 3, 4, 5, 6, 7] },
        { "moment=gt.2026-11-25", [8] },
        { "moment=lt.2026-11-25", [] },
        // not. keeps NULL, as every negated comparison does.
        { "moment=not.eq.2026-11-25T12:30", [1, 2, 6, 7, 8, 9] },
        { "moment=not.lt.2026-11-25T12:30", [3, 4, 5, 6, 7, 8, 9] },
        // Inside a group.
        { "and=(moment.gte.2026-11-25T12:30,moment.lt.2026-11-25T12:31)", [3, 4, 5] },
        { "or=(moment.eq.2026-11-25T12:29:59.999,moment.gte.2026-11-26)", [2, 8] },
        // Without the attribute the date part is compared, as before: the time is ignored.
        { "plain=eq.2026-11-25T12:30:00", [1, 2, 3, 4, 5, 6, 7] },
        { "plain=gt.2026-11-25T12:30:00", [8] },
        // On a DateOnly the attribute changes nothing.
        { "day=eq.2026-11-25", [1, 2, 3, 4, 5, 6, 7] },
    };

    public const string PostgreSqlCreateTable =
        $"CREATE TABLE IF NOT EXISTS {Table} (id integer PRIMARY KEY, moment timestamp NULL, plain timestamp NOT NULL, day date NOT NULL)";

    public const string SqlServerCreateTable =
        $"IF OBJECT_ID('{Table}') IS NULL CREATE TABLE {Table} (id int PRIMARY KEY, moment datetime2(3) NULL, plain datetime2(3) NOT NULL, day date NOT NULL)";

    public const string DuckDbCreateTable =
        $"CREATE TABLE IF NOT EXISTS {Table} (id INTEGER PRIMARY KEY, moment TIMESTAMP, plain TIMESTAMP NOT NULL, day DATE NOT NULL)";

    public const string Insert = $"INSERT INTO {Table} (id, moment, plain, day) VALUES (@Id, @Moment, @Plain, @Day)";
}

/// <summary>
/// [CompareTime] compares a filter value as the interval of its precision, the same rows on every
/// provider; a property without it still compares the date part.
/// </summary>
public abstract class CompareTimeTestsBase
{
    protected abstract Task<IFilterProvider> CreateProviderAsync();

    [Theory]
    [MemberData(nameof(TimeRowData.Cases), MemberType = typeof(TimeRowData))]
    public async Task A_filter_selects_the_rows_of_its_interval(string filter, int[] expected)
    {
        var provider = await CreateProviderAsync();

        var result = await provider.GetAsync<TimeRow>(TimeRowData.Table, FilteredDataQuery.Create($"{filter}&paging=(limit.100)"));

        result.Data.Select(r => r.Id).Should().BeEquivalentTo(expected);
    }

    [Theory]
    [InlineData("moment=eq.2026-11-25T12")]
    [InlineData("moment=eq.25.11.2026")]
    public async Task A_value_in_no_known_format_is_refused(string filter)
    {
        var provider = await CreateProviderAsync();

        var act = () => provider.GetAsync<TimeRow>(TimeRowData.Table, FilteredDataQuery.Create(filter));

        await act.Should().ThrowAsync<Exception>();
    }
}

[Collection("NullKeyset")]
[Trait("Category", "Integration")]
[Trait("Database", "PostgreSql")]
public class CompareTimePostgreSqlTests(NullKeysetPostgreSqlFixture fixture) : CompareTimeTestsBase, IClassFixture<NullKeysetPostgreSqlFixture>
{
    protected override async Task<IFilterProvider> CreateProviderAsync()
    {
        await using (var connection = new NpgsqlConnection(fixture.ConnectionString))
        {
            await connection.ExecuteAsync(TimeRowData.PostgreSqlCreateTable);
            if (await connection.ExecuteScalarAsync<int>($"SELECT count(*) FROM {TimeRowData.Table}") == 0)
                await connection.ExecuteAsync(TimeRowData.Insert, TimeRowData.Rows.Select(r => new { r.Id, r.Moment, r.Plain, Day = r.Day.ToDateTime(TimeOnly.MinValue) }));
        }

        var services = new ServiceCollection();
        services.AddPagin8();
        services.AddPagin8Backend(fixture.ConnectionString);
        return services.BuildServiceProvider().CreateScope().ServiceProvider.GetRequiredService<IFilterProvider>();
    }
}

[Collection("NullKeyset")]
[Trait("Category", "Integration")]
[Trait("Database", "SqlServer")]
public class CompareTimeSqlServerTests(NullKeysetSqlServerFixture fixture) : CompareTimeTestsBase, IClassFixture<NullKeysetSqlServerFixture>
{
    protected override async Task<IFilterProvider> CreateProviderAsync()
    {
        await using (var connection = new SqlConnection(fixture.ConnectionString))
        {
            await connection.ExecuteAsync(TimeRowData.SqlServerCreateTable);
            if (await connection.ExecuteScalarAsync<int>($"SELECT count(*) FROM {TimeRowData.Table}") == 0)
                await connection.ExecuteAsync(TimeRowData.Insert, TimeRowData.Rows.Select(r => new
                {
                    r.Id,
                    Moment = new DbDateTime2(r.Moment),
                    Plain = new DbDateTime2(r.Plain),
                    Day = r.Day.ToDateTime(TimeOnly.MinValue),
                }));
        }

        var services = new ServiceCollection();
        services.AddPagin8Sql();
        services.AddPagin8BackendSqlServer(fixture.ConnectionString);
        return services.BuildServiceProvider().CreateScope().ServiceProvider.GetRequiredService<ISqlServerFilterProvider>();
    }

    // Dapper sends a DateTime as datetime, which would round the milliseconds of the seed rows.
    private sealed class DbDateTime2(DateTime? value) : SqlMapper.ICustomQueryParameter
    {
        public void AddParameter(System.Data.IDbCommand command, string name)
        {
            var parameter = new SqlParameter(name, System.Data.SqlDbType.DateTime2) { Value = (object?)value ?? DBNull.Value };
            command.Parameters.Add(parameter);
        }
    }
}

[Collection("NullKeyset")]
[Trait("Category", "Integration")]
[Trait("Database", "DuckDb")]
public class CompareTimeDuckDbTests(NullKeysetDuckDbFixture fixture) : CompareTimeTestsBase, IClassFixture<NullKeysetDuckDbFixture>
{
    protected override async Task<IFilterProvider> CreateProviderAsync()
    {
        await fixture.Connection.ExecuteAsync(TimeRowData.DuckDbCreateTable);
        if (await fixture.Connection.ExecuteScalarAsync<int>($"SELECT count(*) FROM {TimeRowData.Table}") == 0)
        {
            foreach (var row in TimeRowData.Rows)
            {
                await using var command = fixture.Connection.CreateCommand();
                command.CommandText = $"INSERT INTO {TimeRowData.Table} VALUES ($1, $2, $3, $4)";
                foreach (var value in new object?[] { row.Id, row.Moment, row.Plain, row.Day })
                    command.Parameters.Add(new DuckDBParameter(value ?? DBNull.Value));
                await command.ExecuteNonQueryAsync();
            }
        }

        var services = new ServiceCollection();
        services.AddPagin8();
        services.AddPagin8BackendDuckDb(_ => fixture.CreateConnection(), options => options.CommandTimeout = 30);
        return services.BuildServiceProvider().CreateScope().ServiceProvider.GetRequiredService<IDuckDbFilterProvider>();
    }
}

/// <summary>The same cases in LINQ mode, over the rows in memory.</summary>
public class CompareTimeLinqTests : Pagin8TestBase
{
    [Theory]
    [MemberData(nameof(TimeRowData.Cases), MemberType = typeof(TimeRowData))]
    public void A_filter_selects_the_rows_of_its_interval(string filter, int[] expected)
    {
        var visitor = new LinqTokenVisitor<TimeRow>(new Pagin8MetadataProvider(new MetadataProvider()), new DateProcessor());
        var rows = TimeRowData.Rows.AsQueryable();

        foreach (var token in new Tokenizer().Tokenize(filter))
            rows = token.Accept(visitor, rows);

        rows.Select(r => r.Id).Should().BeEquivalentTo(expected);
    }
}

/// <summary>Column metadata tells a client which properties compare the time.</summary>
public class CompareTimeMetadataTests
{
    [Fact]
    public void Only_a_DateTime_with_CompareTime_has_the_flag()
    {
        var meta = new Pagin8MetadataProvider(new MetadataProvider()).Get(typeof(TimeRow)).ToList();

        meta.Single(c => c.Name == "moment").Flags.Should().Contain("compare-time");
        meta.Single(c => c.Name == "plain").Flags.Should().NotContain("compare-time");
        meta.Single(c => c.Name == "day").Flags.Should().NotContain("compare-time").And.Contain("date-only");
    }
}
