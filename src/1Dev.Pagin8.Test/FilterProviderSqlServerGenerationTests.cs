using _1Dev.Pagin8.Input;
using _1Dev.Pagin8.Internal;
using _1Dev.Pagin8.Internal.DateProcessor;
using _1Dev.Pagin8.Internal.Metadata;
using _1Dev.Pagin8.Internal.Metadata.Models;
using _1Dev.Pagin8.Internal.Tokenizer;
using _1Dev.Pagin8.Internal.Visitors;
using _1Dev.Pagin8.Test.SqlQueryBuilderTests.Internal;
using FluentAssertions;

namespace _1Dev.Pagin8.Test;

/// <summary>
/// Verifies that filter query strings are translated into correct SQL Server WHERE/ORDER BY/OFFSET
/// fragments without a real database connection.  Mirrors FilterProviderSqlGenerationTests but for
/// the SQL Server visitor — key differences: LIKE (not ILIKE), OFFSET...FETCH NEXT (not LIMIT),
/// string IN uses literal IN(...) rather than ANY(ARRAY[...]).
/// </summary>
[Collection("SQL Server QueryBuilder Tests")]
public class FilterProviderSqlServerGenerationTests
{
    private readonly ISqlQueryBuilder _sut;

    public FilterProviderSqlServerGenerationTests()
    {
        SqlServerTestBootstrap.Init();

        var tokenizer        = new Tokenizer();
        var contextValidator = new PassThroughContextValidator();
        var metadataProvider = new Pagin8MetadataProvider(new MetadataProvider());
        var dateProcessor    = new DateProcessor();
        var tokenizationSvc  = new TokenizationService(tokenizer, contextValidator, metadataProvider);
        var tokenVisitor     = new SqlServerTokenVisitor(metadataProvider, dateProcessor);

        _sut = new SqlQueryBuilder(tokenizationSvc, tokenVisitor);
    }

    // -----------------------------------------------------------------------
    // Helper — mirrors FilterProvider's QueryBuilderParameters construction
    // -----------------------------------------------------------------------
    private static QueryBuilderParameters Build(
        string queryString,
        string defaultQuery = "",
        bool   ignoreLimit  = false,
        bool   isJson       = false,
        bool   isCount      = false)
        => new()
        {
            InputParameters = QueryInputParameters.Create(
                sql:                "SELECT * FROM vw_test",
                queryString:        queryString,
                defaultQueryString: defaultQuery,
                ignoreLimit:        ignoreLimit,
                isJson:             isJson,
                isCount:            isCount)
        };

    // -----------------------------------------------------------------------
    // Basic filter operators
    // -----------------------------------------------------------------------

    [Fact]
    public void Should_GenerateLikeClause_WhenEqualityFilterProvided()
    {
        var result = _sut.BuildSqlQuery<TestEntity>(Build("name=eq.Alice"));

        result.Builder.Should().NotBeNull();
        var sql = result.Builder!.AsSql().Sql;

        sql.Should().Contain("name LIKE @p0");
        sql.Should().Contain("ORDER BY id ASC");
        sql.Should().Contain("OFFSET");
    }

    [Fact]
    public void Should_LowercaseStringParameter_WhenEqualityFilterProvided()
    {
        var result = _sut.BuildSqlQuery<TestEntity>(Build("name=eq.Alice"));

        var p = result.Builder!.Build().SqlParameters;
        p[0].Argument.Should().Be("alice");   // Pagin8 lower-cases string params
    }

    [Fact]
    public void Should_GenerateWildcardPattern_WhenContainsOperatorUsed()
    {
        var result = _sut.BuildSqlQuery<TestEntity>(Build("name=cs.test"));

        var sql = result.Builder!.AsSql().Sql;
        sql.Should().Contain("LIKE");

        var p = result.Builder.Build().SqlParameters;
        p[0].Argument.Should().Be("%test%");
    }

    [Fact]
    public void Should_GenerateGreaterThanClause_WhenGtOperatorOnNumericField()
    {
        var result = _sut.BuildSqlQuery<TestEntity>(Build("amount=gt.100"));

        var sql = result.Builder!.AsSql().Sql;
        sql.Should().Contain("amount > @p0");
    }

    [Fact]
    public void Should_GenerateInClause_WhenInOperatorOnNumericField()
    {
        var result = _sut.BuildSqlQuery<TestEntity>(Build("id=in.(1,2,3)"));

        var sql = result.Builder!.AsSql().Sql;
        sql.Should().Contain("id IN (1,2,3)");
    }

    [Fact]
    public void Should_GenerateInClauseNotAnyArray_WhenInOperatorOnStringField()
    {
        var result = _sut.BuildSqlQuery<TestEntity>(Build("status=in.(active,pending)"));

        var sql = result.Builder!.AsSql().Sql;
        sql.Should().Contain("status");
        sql.Should().Contain("IN");
        sql.Should().NotContain("ANY(ARRAY[");
    }

    [Fact]
    public void Should_GenerateOrClause_WhenOrGroupProvided()
    {
        var result = _sut.BuildSqlQuery<TestEntity>(Build("or=(name.cs.foo,status.eq.bar)"));

        var sql = result.Builder!.AsSql().Sql;
        sql.Should().Contain("OR");
        sql.Should().Contain("name");
        sql.Should().Contain("status");
    }

    [Fact]
    public void Should_GenerateAndClause_WhenAndGroupProvided()
    {
        var result = _sut.BuildSqlQuery<TestEntity>(Build("and=(name.cs.foo,status.eq.bar)"));

        var sql = result.Builder!.AsSql().Sql;
        sql.Should().Contain("AND");
        sql.Should().Contain("name");
        sql.Should().Contain("status");
    }

    // -----------------------------------------------------------------------
    // DefaultQuery
    // -----------------------------------------------------------------------

    [Fact]
    public void Should_ApplyDefaultQuery_WhenQueryStringIsEmpty()
    {
        var result = _sut.BuildSqlQuery<TestEntity>(Build(
            queryString:  "",
            defaultQuery: "status=eq.active"));

        var sql = result.Builder!.AsSql().Sql;
        sql.Should().Contain("status");
    }

    [Fact]
    public void Should_IgnoreDefaultQuery_WhenUserProvidesFilter()
    {
        var result = _sut.BuildSqlQuery<TestEntity>(Build(
            queryString:  "name=cs.foo",
            defaultQuery: "amount=gt.0"));

        var sql = result.Builder!.AsSql().Sql;
        sql.Should().Contain("name");
        sql.Should().NotContain("amount");
    }

    // -----------------------------------------------------------------------
    // Sorting
    // -----------------------------------------------------------------------

    [Fact]
    public void Should_GenerateOrderByAsc_WhenSortAscending()
    {
        var result = _sut.BuildSqlQuery<TestEntity>(Build("paging=(sort(name.asc))"));

        result.Builder!.AsSql().Sql.Should().Contain("ORDER BY name ASC");
    }

    [Fact]
    public void Should_GenerateOrderByDesc_WhenSortDescending()
    {
        var result = _sut.BuildSqlQuery<TestEntity>(Build("paging=(sort(amount.desc))"));

        result.Builder!.AsSql().Sql.Should().Contain("ORDER BY amount DESC");
    }

    // -----------------------------------------------------------------------
    // Paging — SQL Server uses OFFSET...FETCH NEXT instead of LIMIT
    // -----------------------------------------------------------------------

    [Fact]
    public void Should_UseOffsetFetchNextNotLimit_WhenPaging()
    {
        var result = _sut.BuildSqlQuery<TestEntity>(Build("name=eq.Alice"));

        var sql = result.Builder!.AsSql().Sql;
        sql.Should().Contain("OFFSET");
        sql.Should().Contain("ROWS FETCH NEXT");
        sql.Should().Contain("ROWS ONLY");
        sql.Should().NotContain("LIMIT");
    }

    [Fact]
    public void Should_ApplyDefaultPageLimit_WhenFilterProvided()
    {
        var result = _sut.BuildSqlQuery<TestEntity>(Build("name=eq.Alice"));

        var p = result.Builder!.Build().SqlParameters;
        p.Should().Contain(sp => sp.Argument.Equals(50));
    }

    [Fact]
    public void Should_UseProvidedLimit_WhenCustomPagingSpecified()
    {
        var result = _sut.BuildSqlQuery<TestEntity>(Build("paging=(sort(id.asc),limit.10)"));

        var sql = result.Builder!.AsSql().Sql;
        sql.Should().Contain("FETCH NEXT");
        var p = result.Builder.Build().SqlParameters;
        p.Should().Contain(sp => sp.Argument.Equals(10));
    }

    [Fact]
    public void Should_SetLimitToMaxSafeItemCount_WhenIgnoreLimitEnabled()
    {
        var result = _sut.BuildSqlQuery<TestEntity>(
            Build("name=eq.Alice", ignoreLimit: true));

        var sql = result.Builder!.AsSql().Sql;
        sql.Should().Contain("FETCH NEXT");
        var p = result.Builder.Build().SqlParameters;
        p.Should().Contain(sp => sp.Argument.Equals(1_000_000));
    }

    // -----------------------------------------------------------------------
    // isJson flag — wrapper is emitted by SqlQueryBuilder (not the visitor),
    // so json_agg wrapping applies to both PostgreSQL and SQL Server.
    // -----------------------------------------------------------------------

    [Fact]
    public void Should_WrapResultInJsonAgg_WhenIsJsonTrue()
    {
        var result = _sut.BuildSqlQuery<TestEntity>(
            Build("name=eq.Alice", isJson: true));

        result.Builder!.AsSql().Sql.Should().Contain("json_agg");
        result.Builder.AsSql().Sql.Should().Contain("'[]'");
    }

    [Fact]
    public void Should_NotWrapInJsonAgg_WhenIsJsonFalse()
    {
        var result = _sut.BuildSqlQuery<TestEntity>(
            Build("name=eq.Alice", isJson: false));

        result.Builder!.AsSql().Sql.Should().NotContain("json_agg");
    }

    // -----------------------------------------------------------------------
    // Count query
    // -----------------------------------------------------------------------

    [Fact]
    public void Should_NotContainFetchNext_WhenCountQueryRequested()
    {
        var result = _sut.BuildSqlQuery<TestEntity>(
            Build("name=eq.Alice", isCount: true));

        var sql = result.Builder?.AsSql().Sql ?? string.Empty;
        sql.Should().NotContain("FETCH NEXT");
    }

    // -----------------------------------------------------------------------
    // Meta
    // -----------------------------------------------------------------------

    [Fact]
    public void Should_HaveShowCountFalse_WhenNoCountTokenInQuery()
    {
        var result = _sut.BuildSqlQuery<TestEntity>(Build("name=eq.Alice"));

        result.Meta.ShowCount.Should().BeFalse();
    }

    [Fact]
    public void Should_ProduceBuilder_WhenFilterIsEmpty()
    {
        var result = _sut.BuildSqlQuery<TestEntity>(Build(""));

        result.Builder.Should().NotBeNull();
    }
}
