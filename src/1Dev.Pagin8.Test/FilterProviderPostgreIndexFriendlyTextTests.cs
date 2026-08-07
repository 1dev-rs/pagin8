using _1Dev.Pagin8.Input;
using _1Dev.Pagin8.Internal;
using _1Dev.Pagin8.Internal.Configuration;
using _1Dev.Pagin8.Internal.DateProcessor;
using _1Dev.Pagin8.Internal.Metadata;
using _1Dev.Pagin8.Internal.Metadata.Models;
using _1Dev.Pagin8.Internal.Tokenizer;
using _1Dev.Pagin8.Internal.Visitors;
using _1Dev.Pagin8.Test.SqlQueryBuilderTests.Internal;
using FluentAssertions;
using Internal.Configuration;

namespace _1Dev.Pagin8.Test;

/// <summary>
/// ServiceConfiguration.UseIndexFriendlyTextComparisons opt-in: eq/stw on text columns should emit
/// lower(column) = / LIKE instead of ILIKE, sargable against a plain lower(column) btree index.
/// cs/enw are unaffected either way — no rewrite makes a substring/suffix scan index-friendly.
/// Same collection as FilterProviderPostgreGenerationTests so Pagin8Runtime's process-global state
/// never races between the flag-off and flag-on tests.
/// </summary>
[Collection("PostgreSQL QueryBuilder Tests")]
public class FilterProviderPostgreIndexFriendlyTextTests
{
    private readonly ISqlQueryBuilder _sut;

    public FilterProviderPostgreIndexFriendlyTextTests()
    {
        Pagin8Runtime.Initialize(new ServiceConfiguration
        {
            MaxNestingLevel = 5,
            PagingSettings = new PagingSettings { DefaultPerPage = 50, MaxItemsPerPage = 5000, MaxSafeItemCount = 1_000_000 },
            DatabaseType = DatabaseType.PostgreSql,
            UseIndexFriendlyTextComparisons = true
        });

        var tokenizer        = new Tokenizer();
        var contextValidator = new PassThroughContextValidator();
        var metadataProvider = new Pagin8MetadataProvider(new MetadataProvider());
        var dateProcessor    = new DateProcessor();
        var tokenizationSvc  = new TokenizationService(tokenizer, contextValidator, metadataProvider);
        var tokenVisitor     = new NpgsqlTokenVisitor(metadataProvider, dateProcessor);

        _sut = new SqlQueryBuilder(tokenizationSvc, tokenVisitor);
    }

    private static QueryBuilderParameters Build(string queryString, string defaultQuery = "") => new()
    {
        InputParameters = QueryInputParameters.Create(
            sql: "SELECT * FROM vw_test",
            queryString: queryString,
            defaultQueryString: defaultQuery,
            ignoreLimit: false,
            isJson: false,
            ignorePaging: false)
    };

    [Fact]
    public void Should_GenerateLowerEqualsClause_WhenEqualityFilterProvided()
    {
        var result = _sut.BuildSqlQuery<TestEntity>(Build("name=eq.Alice"));
        var sql = result.Builder!.AsSql().Sql;

        sql.Should().Contain("lower(name) = @p0");
        sql.Should().NotContain("ILIKE");
    }

    [Fact]
    public void Should_GenerateNotEqualsClause_WhenNegatedEqualityFilterProvided()
    {
        var result = _sut.BuildSqlQuery<TestEntity>(Build("name=not.eq.Alice"));
        var sql = result.Builder!.AsSql().Sql;

        sql.Should().Contain("lower(name) != @p0");
        sql.Should().NotContain("ILIKE");
    }

    [Fact]
    public void Should_GenerateLowerLikeClause_WhenStartsWithFilterProvided()
    {
        var result = _sut.BuildSqlQuery<TestEntity>(Build("name=stw.Ali"));
        var sql = result.Builder!.AsSql().Sql;

        sql.Should().Contain("lower(name) LIKE @p0");
        sql.Should().NotContain("ILIKE");

        var p = result.Builder.Build().SqlParameters;
        p[0].Argument.Should().Be("ali%");
    }

    [Fact]
    public void Should_StillGenerateIlikeWildcardPattern_WhenContainsOperatorUsed()
    {
        // cs/enw are untouched by the flag — substring/suffix search has no sargable rewrite here.
        var result = _sut.BuildSqlQuery<TestEntity>(Build("name=cs.test"));
        var sql = result.Builder!.AsSql().Sql;

        sql.Should().Contain("name ILIKE @p0");
        sql.Should().NotContain("lower(name)");

        var p = result.Builder.Build().SqlParameters;
        p[0].Argument.Should().Be("%test%");
    }

    [Fact]
    public void Should_UseEqualsOnLoweredColumn_ForTextInFilter()
    {
        var result = _sut.BuildSqlQuery<TestEntity>(Build("name=in.(Alice,Bob)"));
        var sql = result.Builder!.AsSql().Sql;

        sql.Should().Contain("lower(name)");
        sql.Should().Contain("ANY(ARRAY[");
        sql.Should().NotContain("ILIKE");
    }
}
