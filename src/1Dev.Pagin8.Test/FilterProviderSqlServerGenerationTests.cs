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
        bool   ignorePaging = false)
        => new()
        {
            InputParameters = QueryInputParameters.Create(
                sql:                "SELECT * FROM vw_test",
                queryString:        queryString,
                defaultQueryString: defaultQuery,
                ignoreLimit:        ignoreLimit,
                isJson:             isJson,
                ignorePaging:       ignorePaging)
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

    [Fact]
    public void Should_NotEmitOrderBySelectNull_WhenSortIsExplicit()
    {
        // When an explicit sort is present the fallback ORDER BY (SELECT NULL) must NOT appear.
        var result = _sut.BuildSqlQuery<TestEntity>(Build("paging=(sort(id.asc))"));

        var sql = result.Builder!.AsSql().Sql;
        sql.Should().Contain("ORDER BY id ASC");
        sql.Should().NotContain("ORDER BY (SELECT NULL)");
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
            Build("name=eq.Alice", ignorePaging: true));

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

    // -----------------------------------------------------------------------
    // stw.in / enw.in -- LIKE-family InToken on text fields
    // -----------------------------------------------------------------------

    [Fact]
    public void Should_GenerateLikeClauses_WhenStwInFilterProvided()
    {
        // name=stw.in.(Alice,Bob) should generate OR-connected LIKE conditions, not IN
        var result = _sut.BuildSqlQuery<TestEntity>(Build("name=stw.in.(Alice,Bob)"));

        var sql = result.Builder!.AsSql().Sql;

        sql.Should().Contain("LOWER(name) LIKE @p0");
        sql.Should().Contain("LOWER(name) LIKE @p1");
        sql.Should().Contain(" OR ");
        sql.Should().NotContain(" IN (");
    }

    [Fact]
    public void Should_AppendWildcardToParameters_WhenStwInFilterProvided()
    {
        var result = _sut.BuildSqlQuery<TestEntity>(Build("name=stw.in.(Alice,Bob)"));

        var p = result.Builder!.Build().SqlParameters;
        p.Should().Contain(sp => (sp.Argument as string) != null && ((string)sp.Argument).EndsWith("%"));
    }

    [Fact]
    public void Should_GenerateNotLikeClauses_WhenNotStwInFilterProvided()
    {
        // name=not.stw.in.(EPP,TAR) should generate NOT (LOWER(col) LIKE ... OR ...) -- not NOT IN
        var result = _sut.BuildSqlQuery<TestEntity>(Build("name=not.stw.in.(EPP,TAR)"));

        var sql = result.Builder!.AsSql().Sql;

        sql.Should().Contain("NOT (");
        sql.Should().Contain("LOWER(name) LIKE @p0");
        sql.Should().Contain("LOWER(name) LIKE @p1");
        sql.Should().NotContain("NOT IN");
    }

    [Fact]
    public void Should_GenerateNotLikeClauses_WhenNestedNotStwInFilterProvided()
    {
        // Nested form: or=(and(name.not.stw.in.(EPP,TAR))) -- this is the real-world trigger
        var result = _sut.BuildSqlQuery<TestEntity>(
            Build("or=(and(name.not.stw.in.(EPP,TAR)))"));

        var sql = result.Builder!.AsSql().Sql;

        sql.Should().Contain("NOT (");
        sql.Should().Contain("LOWER(name) LIKE @p0");
        sql.Should().Contain("LOWER(name) LIKE @p1");
        sql.Should().NotContain("NOT IN");
    }

    // -----------------------------------------------------------------------
    // Additional comparison operators
    // -----------------------------------------------------------------------

    [Fact]
    public void Should_GenerateLessThanClause_WhenLtOperatorOnNumericField()
    {
        var result = _sut.BuildSqlQuery<TestEntity>(Build("amount=lt.100"));

        result.Builder!.AsSql().Sql.Should().Contain("amount < @p0");
    }

    [Fact]
    public void Should_GenerateLessThanOrEqualClause_WhenLteOperatorOnNumericField()
    {
        var result = _sut.BuildSqlQuery<TestEntity>(Build("amount=lte.100"));

        result.Builder!.AsSql().Sql.Should().Contain("amount <= @p0");
    }

    [Fact]
    public void Should_GenerateGreaterThanOrEqualClause_WhenGteOperatorOnNumericField()
    {
        var result = _sut.BuildSqlQuery<TestEntity>(Build("amount=gte.100"));

        result.Builder!.AsSql().Sql.Should().Contain("amount >= @p0");
    }

    [Fact]
    public void Should_GenerateStartsWithPattern_WhenStwOperatorUsed()
    {
        var result = _sut.BuildSqlQuery<TestEntity>(Build("name=stw.alice"));

        var sql = result.Builder!.AsSql().Sql;
        sql.Should().Contain("LIKE");

        var p = result.Builder.Build().SqlParameters;
        p[0].Argument.Should().Be("alice%");
    }

    [Fact]
    public void Should_GenerateEndsWithPattern_WhenEnwOperatorUsed()
    {
        var result = _sut.BuildSqlQuery<TestEntity>(Build("name=enw.alice"));

        var sql = result.Builder!.AsSql().Sql;
        sql.Should().Contain("LIKE");

        var p = result.Builder.Build().SqlParameters;
        p[0].Argument.Should().Be("%alice");
    }

    [Fact]
    public void Should_GenerateLikeClause_WhenLikeOperatorUsed()
    {
        // like operator uses LIKE without adding wildcards — the caller supplies the pattern
        var result = _sut.BuildSqlQuery<TestEntity>(Build("name=like.alice"));

        var sql = result.Builder!.AsSql().Sql;
        sql.Should().Contain("LIKE");

        var p = result.Builder.Build().SqlParameters;
        p[0].Argument.Should().Be("alice");
    }

    // -----------------------------------------------------------------------
    // Is operator (SQL Server specific bool translation)
    // -----------------------------------------------------------------------

    [Fact]
    public void Should_GenerateIsNullOrEmptyClause_WhenIsEmptyOnTextField()
    {
        // is.$empty on a text field → field IS NULL OR field = ''
        var result = _sut.BuildSqlQuery<TestEntity>(Build("name=is.$empty"));

        var sql = result.Builder!.AsSql().Sql;
        sql.Should().Contain("IS NULL");
        sql.Should().Contain("= ''");
        sql.Should().Contain("OR");
    }

    [Fact]
    public void Should_GenerateIsNullClause_WhenIsEmptyOnNumericField()
    {
        // is.$empty on a non-text field → only NULL check
        var result = _sut.BuildSqlQuery<TestEntity>(Build("amount=is.$empty"));

        var sql = result.Builder!.AsSql().Sql;
        sql.Should().Contain("IS");
        sql.Should().Contain("NULL");
        sql.Should().NotContain("= ''");
    }

    [Fact]
    public void Should_GenerateIsNotNullAndNotEmptyClause_WhenNegatedIsEmptyOnTextField()
    {
        // is.not.$empty on a text field → field IS NOT NULL AND field <> ''
        var result = _sut.BuildSqlQuery<TestEntity>(Build("name=is.not.$empty"));

        var sql = result.Builder!.AsSql().Sql;
        sql.Should().Contain("IS NOT NULL");
        sql.Should().Contain("<> ''");
        sql.Should().Contain("AND");
    }

    [Fact]
    public void Should_GenerateEqualOne_WhenIsTrueOnBoolField()
    {
        // SQL Server: IS TRUE → = @p (bit param = 1); no IS TRUE literal support
        var result = _sut.BuildSqlQuery<TestEntity>(Build("isActive=is.true"));

        var sql = result.Builder!.AsSql().Sql;
        sql.Should().Contain("isActive");
        sql.Should().Contain("= @p");

        var p = result.Builder.Build().SqlParameters;
        p.Should().Contain(sp => sp.Argument.Equals(1));
    }

    [Fact]
    public void Should_GenerateEqualZero_WhenIsFalseOnBoolField()
    {
        // SQL Server: IS FALSE → = @p (bit param = 0)
        var result = _sut.BuildSqlQuery<TestEntity>(Build("isActive=is.false"));

        var sql = result.Builder!.AsSql().Sql;
        sql.Should().Contain("isActive");
        sql.Should().Contain("= @p");

        var p = result.Builder.Build().SqlParameters;
        p.Should().Contain(sp => sp.Argument.Equals(0));
    }

    [Fact]
    public void Should_GenerateNotEqualOne_WhenNegatedIsTrueOnBoolField()
    {
        // SQL Server: IS NOT TRUE → <> @p (bit param = 1)
        var result = _sut.BuildSqlQuery<TestEntity>(Build("isActive=is.not.true"));

        var sql = result.Builder!.AsSql().Sql;
        sql.Should().Contain("isActive");
        sql.Should().Contain("<> @p");

        var p = result.Builder.Build().SqlParameters;
        p.Should().Contain(sp => sp.Argument.Equals(1));
    }

    // -----------------------------------------------------------------------
    // Negated operators
    // -----------------------------------------------------------------------

    [Fact]
    public void Should_WrapInNullCheck_WhenNotEqualityOnTextField()
    {
        // not.eq on text → NOT LIKE wrapped with OR IS NULL
        var result = _sut.BuildSqlQuery<TestEntity>(Build("name=not.eq.Alice"));

        var sql = result.Builder!.AsSql().Sql;
        sql.Should().Contain("NOT LIKE");
        sql.Should().Contain("IS NULL");
    }

    [Fact]
    public void Should_WrapInNullCheck_WhenNotContainsOnTextField()
    {
        // not.cs on text → NOT LIKE with wildcard, wrapped with OR IS NULL
        var result = _sut.BuildSqlQuery<TestEntity>(Build("name=not.cs.test"));

        var sql = result.Builder!.AsSql().Sql;
        sql.Should().Contain("NOT LIKE");
        sql.Should().Contain("IS NULL");

        var p = result.Builder.Build().SqlParameters;
        p[0].Argument.Should().Be("%test%");
    }

    [Fact]
    public void Should_GenerateNotInWithLower_WhenNotInOperatorOnStringField()
    {
        // not.in on text → LOWER(col) NOT IN(...) wrapped with OR IS NULL
        var result = _sut.BuildSqlQuery<TestEntity>(Build("name=not.in.(alice,bob)"));

        var sql = result.Builder!.AsSql().Sql;
        sql.Should().Contain("NOT IN");
        sql.Should().Contain("IS NULL");
    }

    [Fact]
    public void Should_GenerateNotInClause_WhenNotInOperatorOnNumericField()
    {
        // not.in on a numeric field → standard NOT IN(...) wrapped with OR IS NULL
        var result = _sut.BuildSqlQuery<TestEntity>(Build("id=not.in.(1,2,3)"));

        var sql = result.Builder!.AsSql().Sql;
        sql.Should().Contain("NOT IN");
        sql.Should().Contain("IS NULL");
    }

    // -----------------------------------------------------------------------
    // Date range
    // -----------------------------------------------------------------------

    [Fact]
    public void Should_GenerateBetweenClause_WhenForDateRangeOperator()
    {
        // field=for.7d → BETWEEN <startDate> AND <endDate>
        var result = _sut.BuildSqlQuery<TestEntity>(Build("createdDate=for.7d"));

        var sql = result.Builder!.AsSql().Sql;
        sql.Should().Contain("createdDate");
        sql.Should().Contain("BETWEEN");
        sql.Should().Contain("AND");
    }

    [Fact]
    public void Should_GenerateBetweenClause_WhenAgoDateRangeOperator()
    {
        // field=ago.7d → BETWEEN <startDate> AND <endDate> (direction reversed)
        var result = _sut.BuildSqlQuery<TestEntity>(Build("createdDate=ago.7d"));

        var sql = result.Builder!.AsSql().Sql;
        sql.Should().Contain("createdDate");
        sql.Should().Contain("BETWEEN");
        sql.Should().Contain("AND");
    }

    // -----------------------------------------------------------------------
    // Multiple filters
    // -----------------------------------------------------------------------

    [Fact]
    public void Should_ApplyBothFilters_WhenMultipleFiltersProvidedViaAmpersand()
    {
        var result = _sut.BuildSqlQuery<TestEntity>(Build("name=eq.Alice&amount=gt.100"));

        var sql = result.Builder!.AsSql().Sql;
        sql.Should().Contain("name");
        sql.Should().Contain("amount");
        sql.Should().Contain("LIKE");
        sql.Should().Contain(">");
    }

    // -----------------------------------------------------------------------
    // SQL Server-specific: ORDER BY fallback is covered by TryAddDefault which
    // always injects the primary-key sort when no explicit sort is provided.
    // The ORDER BY (SELECT NULL) guard in the visitor is a defensive safety net
    // for direct PagingToken construction with Sort=null.
    // -----------------------------------------------------------------------

    [Fact]
    public void Should_AlwaysHaveOrderBy_WhenPagingWithLimitButNoExplicitSort()
    {
        // TryAddDefault fills in the primary-key sort whenever none is provided,
        // so even paging=(limit.10) alone still gets ORDER BY id ASC.
        var result = _sut.BuildSqlQuery<TestEntity>(Build("paging=(limit.10)"));

        var sql = result.Builder!.AsSql().Sql;
        sql.Should().Contain("ORDER BY");
        sql.Should().Contain("FETCH NEXT");
        sql.Should().NotContain("ORDER BY (SELECT NULL)");
    }

    // -----------------------------------------------------------------------
    // Negated group
    // -----------------------------------------------------------------------

    [Fact]
    public void Should_NegateGroup_WhenNotAndGroupProvided()
    {
        var result = _sut.BuildSqlQuery<TestEntity>(Build("not.and=(name.cs.foo,status.eq.bar)"));

        var sql = result.Builder!.AsSql().Sql;
        sql.Should().Contain("not");
        sql.Should().Contain("(");
        sql.Should().Contain("name");
        sql.Should().Contain("status");
    }
}
