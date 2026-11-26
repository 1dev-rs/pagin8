using System.Data.Common;
using System.Runtime.CompilerServices;
using _1Dev.Pagin8.Extensions;
using _1Dev.Pagin8.Extensions.Backend.Extensions;
using _1Dev.Pagin8.Extensions.Backend.Interfaces;
using _1Dev.Pagin8.Extensions.Backend.Models;
using _1Dev.Pagin8.Input;
using _1Dev.Pagin8.Internal;
using _1Dev.Pagin8.Internal.Configuration;
using _1Dev.Pagin8.Internal.DateProcessor;
using _1Dev.Pagin8.Internal.Metadata;
using _1Dev.Pagin8.Internal.Metadata.Models;
using _1Dev.Pagin8.Internal.Tokenizer;
using _1Dev.Pagin8.Internal.Tokenizer.Contracts;
using _1Dev.Pagin8.Internal.Visitors;
using _1Dev.Pagin8.Test.SqlQueryBuilderTests.Internal;
using Dapper;
using FluentAssertions;
using Internal.Configuration;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace _1Dev.Pagin8.Test.IntegrationTests.NullKeyset;

/// <summary>
/// Keyset paging over nullable columns, the same on every provider: scrolling page by page to the
/// end must return every row matching the filter exactly once, as many rows as COUNT(*) of that
/// filter in the database, in the order where NULL is the smallest value. A NULL cursor value is
/// sent as $null, as a client must. Before 1.6.5 a page ending on a NULL repeated the NULL rows
/// (or skipped the rest), so the scroll never ended or came back short.
/// </summary>
public abstract class NullKeysetTestsBase
{
    private const int MaxPages = 500;

    /// <summary>A filter as pagin8 query, as plain SQL for COUNT(*) and as a predicate for LINQ.</summary>
    public sealed record Filter(string Query, string Sql, Func<NullRow, bool> Matches);

    public static readonly IReadOnlyDictionary<string, Filter> Filters = new Dictionary<string, Filter>
    {
        ["none"] = new("", "1 = 1", _ => true),
        ["amount-positive"] = new("amount=gt.0", "amount > 0", r => r.Amount > 0),
        ["quantity-not-positive"] = new("quantity=lte.0", "quantity <= 0", r => r.Quantity <= 0),
        ["code"] = new("code=eq.c1", "code = 'c1'", r => r.Code == "c1"),
        ["label"] = new("label=eq.a", "label = 'a'", r => r.Label == "a"),
    };

    public static TheoryData<string, string, int, string> Walks
    {
        get
        {
            var data = new TheoryData<string, string, int, string>();
            foreach (var column in NullRowData.SortableFields)
            foreach (var direction in new[] { "asc", "desc" })
            {
                foreach (var pageSize in new[] { 1, 3, 7 })
                    data.Add(column, direction, pageSize, "none");
                foreach (var filter in Filters.Keys.Where(k => k != "none"))
                    data.Add(column, direction, 2, filter);
            }

            return data;
        }
    }

    public static TheoryData<string, int, string> MultiColumnWalks
    {
        get
        {
            var data = new TheoryData<string, int, string>();
            foreach (var sort in new[] { "label.asc,quantity.desc,id.asc", "label.desc,amount.asc,id.desc", "flag.asc,shipped.desc,id.desc", "note.desc,quantity.asc,id.asc", "label.asc,quantity.desc", "flag.desc" })
            foreach (var pageSize in new[] { 1, 4 })
            foreach (var filter in new[] { "none", "amount-positive" })
                data.Add(sort, pageSize, filter);
            return data;
        }
    }

    protected abstract Task<List<NullRow>> QueryAsync(string query);

    /// <summary>COUNT(*) of the filter, by the database itself (LINQ: by the predicate).</summary>
    protected abstract Task<int> CountAsync(Filter filter);

    [Theory]
    [MemberData(nameof(Walks))]
    public async Task Scrolling_to_the_end_returns_every_row_once(string field, string direction, int pageSize, string filter) =>
        await AssertScrollAsync($"{field}.{direction},id.{direction}", pageSize, Filters[filter]);

    [Theory]
    [MemberData(nameof(MultiColumnWalks))]
    public async Task Scrolling_by_several_nullable_columns_returns_every_row_once(string sort, int pageSize, string filter) =>
        await AssertScrollAsync(sort, pageSize, Filters[filter]);

    private async Task AssertScrollAsync(string sort, int pageSize, Filter filter)
    {
        var columns = sort.Split(',').Select(s => s.Split('.')).Select(p => (Field: p[0], Direction: p[1])).ToList();
        var count = await CountAsync(filter);
        var expected = NullRowData.ExpectedOrder(NullRowData.Rows.Where(filter.Matches), columns.Select(c => (c.Field, c.Direction == "desc")).ToArray());
        count.Should().Be(expected.Count, "the seeded rows are what the database holds");

        // Without an explicit id the key orders the ties, ascending, as pagin8 always orders $key.
        var keyOnly = columns.All(c => c.Field != "id");
        var walked = new List<int>();
        var cursor = keyOnly ? $"{sort},$key.asc" : sort;
        for (var page = 0; page < MaxPages; page++)
        {
            var query = $"paging=(sort({cursor}),limit.{pageSize})";
            if (filter.Query.Length > 0)
                query = $"{filter.Query}&{query}";

            var rows = await QueryAsync(query);
            if (rows.Count == 0)
                break;

            rows.Count.Should().BeLessThanOrEqualTo(pageSize);
            walked.AddRange(rows.Select(r => r.Id));
            walked.Count.Should().BeLessThanOrEqualTo(count, $"a scroll never returns more rows than COUNT(*) (page {page + 1}: {query})");

            var last = rows[^1];
            cursor = string.Join(",", columns.Select(c => $"{c.Field}.{c.Direction}.{NullRowData.Cursor(NullRowData.Value(last, c.Field))}")) + $",$key.{last.Id}";
        }

        walked.Should().OnlyHaveUniqueItems();
        walked.Should().HaveCount(count, "scrolling to the end returns as many rows as COUNT(*) of the filter");
        walked.Should().Equal(expected, "NULL is the smallest value");
    }

    protected static ServiceConfiguration RuntimeConfiguration(DatabaseType databaseType) => new()
    {
        MaxNestingLevel = 5,
        PagingSettings = new PagingSettings { DefaultPerPage = 50, MaxItemsPerPage = 1000, MaxSafeItemCount = 10_000 },
        DatabaseType = databaseType,
    };

    protected static async Task<List<NullRow>> RunBuilderAsync(DbConnection connection, ISqlTokenVisitor visitor, string query)
    {
        var metadata = new Pagin8MetadataProvider(new MetadataProvider());
        var builder = new SqlQueryBuilder(new TokenizationService(new Tokenizer(), new PassThroughContextValidator(), metadata), visitor);
        var parameters = QueryBuilderParameters.Create(
            connection,
            FormattableStringFactory.Create($"SELECT * FROM {NullRowData.Table} WHERE 1=1"),
            QueryInputParameters.Create(sql: NullRowData.Table, queryString: query, defaultQueryString: "", ignoreLimit: false));

        var built = builder.BuildSqlQuery<NullRow>(parameters).Builder.Build();
        var arguments = new DynamicParameters();
        for (var i = 0; i < built.SqlParameters.Count; i++)
            arguments.Add($"@p{i}", built.SqlParameters[i].Argument);

        return (await connection.QueryAsync<NullRow>(built.Sql, arguments)).ToList();
    }
}

[Collection("NullKeyset")]
[Trait("Category", "Integration")]
[Trait("Database", "PostgreSql")]
public class NullKeysetPostgreSqlTests(NullKeysetPostgreSqlFixture fixture) : NullKeysetTestsBase, IClassFixture<NullKeysetPostgreSqlFixture>
{
    protected override async Task<List<NullRow>> QueryAsync(string query)
    {
        Pagin8Runtime.Initialize(RuntimeConfiguration(DatabaseType.PostgreSql));
        var metadata = new Pagin8MetadataProvider(new MetadataProvider());
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        return await RunBuilderAsync(connection, new NpgsqlTokenVisitor(metadata, new DateProcessor()), query);
    }

    protected override async Task<int> CountAsync(Filter filter)
    {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        return await connection.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM {NullRowData.Table} WHERE {filter.Sql}");
    }

    // The order is the column itself, so an index on (column NULLS FIRST, id) returns it without a
    // sort, in both directions; the old ORDER BY COALESCE(column, ...) could only be sorted.
    [Theory]
    [InlineData("label", "asc", "label.asc,id.asc")]
    [InlineData("label", "desc", "label.desc,id.desc")]
    [InlineData("quantity", "asc", "quantity.asc.$null,id.asc.21,$key.21")]
    [InlineData("quantity", "desc", "quantity.desc.0,id.desc.40,$key.40")]
    [InlineData("shipped", "asc", "shipped.asc.2024-01-02T00:00:00,id.asc.7,$key.7")]
    public async Task An_index_on_the_column_serves_the_order(string field, string direction, string sort)
    {
        Pagin8Runtime.Initialize(RuntimeConfiguration(DatabaseType.PostgreSql));
        var metadata = new Pagin8MetadataProvider(new MetadataProvider());
        var builder = new SqlQueryBuilder(new TokenizationService(new Tokenizer(), new PassThroughContextValidator(), metadata), new NpgsqlTokenVisitor(metadata, new DateProcessor()));

        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await connection.ExecuteAsync($"CREATE INDEX IF NOT EXISTS ix_{field} ON {NullRowData.Table} ({field} NULLS FIRST, id)");
        await connection.ExecuteAsync("SET enable_seqscan = off; SET enable_sort = off; SET enable_bitmapscan = off");

        var parameters = QueryBuilderParameters.Create(
            connection,
            FormattableStringFactory.Create($"SELECT * FROM {NullRowData.Table} WHERE 1=1"),
            QueryInputParameters.Create(sql: NullRowData.Table, queryString: $"paging=(sort({sort}),limit.5)", defaultQueryString: "", ignoreLimit: false));
        var built = builder.BuildSqlQuery<NullRow>(parameters).Builder.Build();
        var arguments = new DynamicParameters();
        for (var i = 0; i < built.SqlParameters.Count; i++)
            arguments.Add($"@p{i}", built.SqlParameters[i].Argument);

        var plan = string.Join("\n", await connection.QueryAsync<string>($"EXPLAIN {built.Sql}", arguments));

        plan.Should().Contain($"Index Scan{(direction == "desc" ? " Backward" : "")} using ix_{field}");
        plan.Should().NotContain("Sort", plan);
    }
}

[Collection("NullKeyset")]
[Trait("Category", "Integration")]
[Trait("Database", "SqlServer")]
public class NullKeysetSqlServerTests(NullKeysetSqlServerFixture fixture) : NullKeysetTestsBase, IClassFixture<NullKeysetSqlServerFixture>
{
    protected override async Task<List<NullRow>> QueryAsync(string query)
    {
        Pagin8Runtime.Initialize(RuntimeConfiguration(DatabaseType.SqlServer));
        var metadata = new Pagin8MetadataProvider(new MetadataProvider());
        await using var connection = new SqlConnection(fixture.ConnectionString);
        return await RunBuilderAsync(connection, new SqlServerTokenVisitor(metadata, new DateProcessor()), query);
    }

    protected override async Task<int> CountAsync(Filter filter)
    {
        await using var connection = new SqlConnection(fixture.ConnectionString);
        return await connection.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM {NullRowData.Table} WHERE {filter.Sql}");
    }
}

[Collection("NullKeyset")]
[Trait("Category", "Integration")]
[Trait("Database", "DuckDb")]
public class NullKeysetDuckDbTests : NullKeysetTestsBase, IClassFixture<NullKeysetDuckDbFixture>
{
    private readonly NullKeysetDuckDbFixture _fixture;
    private readonly IDuckDbFilterProvider _provider;

    public NullKeysetDuckDbTests(NullKeysetDuckDbFixture fixture)
    {
        _fixture = fixture;
        var services = new ServiceCollection();
        services.AddPagin8(cfg =>
        {
            cfg.DatabaseType = DatabaseType.PostgreSql;
            cfg.PagingSettings = RuntimeConfiguration(DatabaseType.PostgreSql).PagingSettings;
        });
        services.AddPagin8BackendDuckDb(fixture.CreateConnection, commandTimeout: 30);
        _provider = services.BuildServiceProvider().CreateScope().ServiceProvider.GetRequiredService<IDuckDbFilterProvider>();
    }

    protected override async Task<List<NullRow>> QueryAsync(string query)
    {
        Pagin8Runtime.Initialize(RuntimeConfiguration(DatabaseType.PostgreSql));
        var result = await _provider.GetAsync<NullRow>(NullRowData.Table, FilteredDataQuery.Create(query));
        return result.Data.ToList();
    }

    protected override async Task<int> CountAsync(Filter filter)
    {
        await using var connection = _fixture.CreateConnection();
        return await connection.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM {NullRowData.Table} WHERE {filter.Sql}");
    }
}

[Collection("NullKeyset")]
[Trait("Category", "Integration")]
[Trait("Database", "Linq")]
public class NullKeysetLinqTests : NullKeysetTestsBase
{
    private readonly IQueryableTokenProcessor<NullRow> _processor;

    public NullKeysetLinqTests()
    {
        var services = new ServiceCollection();
        services.AddPagin8(cfg =>
        {
            cfg.DatabaseType = DatabaseType.PostgreSql;
            cfg.PagingSettings = RuntimeConfiguration(DatabaseType.PostgreSql).PagingSettings;
        });
        _processor = services.BuildServiceProvider().GetRequiredService<IQueryableTokenProcessor<NullRow>>();
    }

    protected override Task<List<NullRow>> QueryAsync(string query)
    {
        Pagin8Runtime.Initialize(RuntimeConfiguration(DatabaseType.PostgreSql));
        return Task.FromResult(_processor.Process(query, NullRowData.Rows).ToList());
    }

    protected override Task<int> CountAsync(Filter filter) => Task.FromResult(NullRowData.Rows.Count(filter.Matches));
}
