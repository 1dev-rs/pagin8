using System.ComponentModel.DataAnnotations;
using System.Globalization;
using _1Dev.Pagin8.Extensions;
using _1Dev.Pagin8.Extensions.Backend.Extensions;
using _1Dev.Pagin8.Extensions.Backend.Interfaces;
using _1Dev.Pagin8.Extensions.Backend.Models;
using _1Dev.Pagin8.Input;
using _1Dev.Pagin8.Internal;
using _1Dev.Pagin8.Internal.Attributes;
using _1Dev.Pagin8.Internal.Configuration;
using Attributes;
using Dapper;
using DuckDB.NET.Data;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace _1Dev.Pagin8.Test.IntegrationTests;

public class TextRow
{
    [Key]
    public int Id { get; set; }

    [NullsAllowed]
    public string? Code { get; set; }
}

public sealed class DuckDbTextMatchFixture : IAsyncLifetime
{
    public static readonly IReadOnlyDictionary<int, string?> Rows = new Dictionary<int, string?>
    {
        [1] = "ABC-123",
        [2] = "abc-123",
        [3] = "123",
        [4] = null,
        [5] = "x_1",
        [6] = "xa1",
        [7] = "x%1",
        [8] = "",
        [9] = "0123",
        [10] = "Abc",
        [11] = "12-3",
    };

    public DuckDBConnection Connection { get; } = new("DataSource=:memory:");

    public async Task InitializeAsync()
    {
        await Connection.OpenAsync();
        await Connection.ExecuteAsync("CREATE TABLE text_rows (id INTEGER PRIMARY KEY, code VARCHAR)");
        foreach (var (id, code) in Rows)
            await Connection.ExecuteAsync("INSERT INTO text_rows VALUES ($id, $code)", new { id, code });
    }

    public Task DisposeAsync()
    {
        Connection.Dispose();
        return Task.CompletedTask;
    }
}

/// <summary>
/// Text filters on DuckDB run as its string functions (contains, starts_with, ends_with, =) instead
/// of ILIKE, and must select exactly the rows ILIKE selected: case-insensitive, the value taken
/// literally (% and _ are not wildcards in cs/stw/enw), NULL rows kept by not. — at any depth of
/// and=/or=, including a text in.(...) inside a group, which DuckDB refused as ILIKE ANY before.
/// Runs under a decimal-comma culture, as on a Serbian Windows machine.
/// </summary>
[Collection("DuckDb")]
[Trait("Category", "Integration")]
[Trait("Database", "DuckDb")]
public class DuckDbTextMatchTests : IClassFixture<DuckDbTextMatchFixture>
{
    private readonly DuckDbTextMatchFixture _fixture;
    private readonly IServiceProvider _services;

    public DuckDbTextMatchTests(DuckDbTextMatchFixture fixture)
    {
        _fixture = fixture;
        var services = new ServiceCollection();
        services.AddPagin8(cfg => cfg.DatabaseType = DatabaseType.PostgreSql);
        services.AddPagin8BackendDuckDb(_ => fixture.Connection.Duplicate(), options => options.CommandTimeout = 30);
        _services = services.BuildServiceProvider().CreateScope().ServiceProvider;
    }

    public static TheoryData<string, int[]> Cases => new()
    {
        { "code=eq.123", [3] },
        { "code=eq.ABC-123", [1, 2] },
        { "code=cs.123", [1, 2, 3, 9] },
        { "code=cs.BC-1", [1, 2] },
        { "code=stw.12", [3, 11] },
        { "code=stw.abc", [1, 2, 10] },
        { "code=enw.123", [1, 2, 3, 9] },
        { "code=enw.-123", [1, 2] },
        { "code=cs.x_1", [5] },
        { "code=cs.x%1", [7] },
        { "code=not.cs.123", [4, 5, 6, 7, 8, 10, 11] },
        { "code=not.eq.123", [1, 2, 4, 5, 6, 7, 8, 9, 10, 11] },
        { "code=not.stw.ab", [3, 4, 5, 6, 7, 8, 9, 11] },
        { "code=in.(123,abc-123)", [1, 2, 3] },
        { "code=not.in.(123,abc-123)", [4, 5, 6, 7, 8, 9, 10, 11] },
        { "or=(code.in.(123,xa1),code.cs.x_1)", [3, 5, 6] },
        { "and=(code.cs.1,code.not.stw.x)", [1, 2, 3, 9, 11] },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Text_filters_select_the_same_rows_as_ILIKE(string query, int[] expected)
    {
        var provider = _services.GetRequiredService<IDuckDbFilterProvider>();

        var culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("sr-Latn-RS");
        try
        {
            var result = await provider.GetAsync<TextRow>("text_rows", FilteredDataQuery.Create($"{query}&paging=(limit.100)"));
            result.Data.Select(r => r.Id).Should().BeEquivalentTo(expected);
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }

    [Theory]
    [InlineData("code=cs.123", "contains(code, @p0)", "123")]
    [InlineData("code=cs.BC-1", "contains(lower(code), @p0)", "bc-1")]
    [InlineData("code=stw.12", "starts_with(code, @p0)", "12")]
    [InlineData("code=enw.Abc", "ends_with(lower(code), @p0)", "abc")]
    [InlineData("code=eq.123", "(code = @p0)", "123")]
    [InlineData("code=cs.x_1", "contains(lower(code), @p0)", "x_1")]
    public void Text_filters_use_string_functions_with_the_literal_value(string query, string expectedSql, string expectedValue)
    {
        var builder = _services.GetRequiredService<IDuckDbSqlQueryBuilder>();

        var result = builder.BuildSqlQuery<TextRow>(new QueryBuilderParameters
        {
            InputParameters = QueryInputParameters.Create(
                sql: "text_rows",
                queryString: query,
                defaultQueryString: "",
                ignoreLimit: false,
                isJson: false,
                ignorePaging: false)
        });

        var sql = result.Builder!.AsSql().Sql;
        sql.Should().Contain(expectedSql);
        sql.Should().NotContain("ILIKE");
        result.Builder.Build().SqlParameters[0].Argument.Should().Be(expectedValue);
    }
}
