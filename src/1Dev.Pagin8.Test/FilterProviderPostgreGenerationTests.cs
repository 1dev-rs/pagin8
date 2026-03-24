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
/// Verifies that filter query strings are translated into correct PostgreSQL WHERE/ORDER BY/LIMIT
/// fragments without a real database connection.  These tests exercise the same pipeline that
/// FilterProvider uses in tar-rest (PostgreSQL variant, isJson flag included).
/// </summary>
[Collection("PostgreSQL QueryBuilder Tests")]
public class FilterProviderPostgreGenerationTests
{
    private readonly ISqlQueryBuilder _sut;

    public FilterProviderPostgreGenerationTests()
    {
        // Ensure PostgreSQL runtime is initialised (re-entrant, safe to call multiple times)
        PostgreSqlTestBootstrap.Init();

        var tokenizer          = new Tokenizer();
        var contextValidator   = new PassThroughContextValidator();
        var metadataProvider   = new Pagin8MetadataProvider(new MetadataProvider());
        var dateProcessor      = new DateProcessor();
        var tokenizationSvc    = new TokenizationService(tokenizer, contextValidator, metadataProvider);
        var tokenVisitor       = new NpgsqlTokenVisitor(metadataProvider, dateProcessor);

        _sut = new SqlQueryBuilder(tokenizationSvc, tokenVisitor);
    }

    // -----------------------------------------------------------------------
    // Helper — builds QueryBuilderParameters the same way FilterProvider does
    // -----------------------------------------------------------------------
    private static QueryBuilderParameters Build(
        string queryString,
        string defaultQuery  = "",
        bool   ignoreLimit   = false,
        bool   isJson        = false,
        bool   ignorePaging  = false)
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
    public void Should_GenerateILikeClause_WhenEqualityFilterProvided()
    {
        var result = _sut.BuildSqlQuery<TestEntity>(Build("name=eq.Alice"));

        result.Builder.Should().NotBeNull();
        var sql = result.Builder!.AsSql().Sql;

        sql.Should().Contain("name ILIKE @p0");
        sql.Should().Contain("ORDER BY id ASC");
        sql.Should().Contain("LIMIT");
    }

    [Fact]
    public void Should_LowercaseStringParameter_WhenEqualityFilterProvided()
    {
        var result = _sut.BuildSqlQuery<TestEntity>(Build("name=eq.Alice"));

        var p = result.Builder!.Build().SqlParameters;
        p[0].Argument.Should().Be("alice");   // Pagin8 lower-cases string params for ILIKE
    }

    [Fact]
    public void Should_GenerateWildcardPattern_WhenContainsOperatorUsed()
    {
        var result = _sut.BuildSqlQuery<TestEntity>(Build("name=cs.test"));

        var sql = result.Builder!.AsSql().Sql;
        sql.Should().Contain("ILIKE");

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
    public void Should_GenerateAnyArrayClause_WhenInOperatorOnStringField()
    {
        // PostgreSQL optimises IN(<list>) to ILIKE ANY(ARRAY[...]) for strings
        var result = _sut.BuildSqlQuery<TestEntity>(Build("status=in.(active,pending)"));

        var sql = result.Builder!.AsSql().Sql;
        // status field is a string on TestEntity — Pagin8 renders it as ANY(ARRAY[...])
        sql.Should().Contain("status");
        sql.Should().Contain("ANY(ARRAY[");
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
        // Key semantic: defaultQuery is a FALLBACK — it is only applied when the user
        // provides NO filter tokens.  When the user provides filters, defaultQuery is
        // completely ignored (replaced, not merged).
        var result = _sut.BuildSqlQuery<TestEntity>(Build(
            queryString:  "name=cs.foo",
            defaultQuery: "amount=gt.0"));

        var sql = result.Builder!.AsSql().Sql;
        // Only the user's filter is present; the default is dropped.
        sql.Should().Contain("name");
        sql.Should().NotContain("amount");
    }

    // -----------------------------------------------------------------------
    // Sorting
    // -----------------------------------------------------------------------

    [Fact]
    public void Should_GenerateOrderByAsc_WhenSortAscending()
    {
        // Sort is expressed inside paging: paging=(sort(field.dir))
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
    // Paging
    // -----------------------------------------------------------------------

    [Fact]
    public void Should_ApplyDefaultPageLimit_WhenFilterProvided()
    {
        // When a filter is provided but no explicit paging, Pagin8 adds a default
        // paging token whose limit = DefaultPerPage (50 from bootstrap config).
        var result = _sut.BuildSqlQuery<TestEntity>(Build("name=eq.Alice"));

        var sql = result.Builder!.AsSql().Sql;
        sql.Should().Contain("LIMIT");
        var p = result.Builder.Build().SqlParameters;
        // Second param is the limit value = DefaultPerPage = 50
        p.Should().Contain(sp => sp.Argument.Equals(50));
    }

    [Fact]
    public void Should_UseProvidedLimit_WhenCustomPagingSpecified()
    {
        // paging=(sort(...),limit.N) — limit is nested inside paging token
        var result = _sut.BuildSqlQuery<TestEntity>(Build("paging=(sort(id.asc),limit.10)"));

        var sql = result.Builder!.AsSql().Sql;
        sql.Should().Contain("LIMIT");
        var p = result.Builder.Build().SqlParameters;
        p.Should().Contain(sp => sp.Argument.Equals(10));
    }

    [Fact]
    public void Should_SetLimitToMaxSafeItemCount_WhenIgnoreLimitEnabled()
    {
        // ignoreLimit: true does NOT remove LIMIT — it overrides the value to
        // MaxSafeItemCount (1_000_000) so that all rows are returned safely.
        var result = _sut.BuildSqlQuery<TestEntity>(
            Build("name=eq.Alice", ignoreLimit: true));

        var sql = result.Builder!.AsSql().Sql;
        sql.Should().Contain("LIMIT");
        var p = result.Builder.Build().SqlParameters;
        p.Should().Contain(sp => sp.Argument.Equals(1_000_000));
    }

    // -----------------------------------------------------------------------
    // isJson flag (critical for tar-rest repos that query JSON columns)
    // -----------------------------------------------------------------------

    [Fact]
    public void Should_WrapResultInJsonAgg_WhenIsJsonTrue()
    {
        var result = _sut.BuildSqlQuery<TestEntity>(
            Build("name=eq.Alice", isJson: true));

        // When isJson:true the entire inner query is wrapped:
        // SELECT COALESCE(json_agg(items), '[]') FROM (<inner>) items
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
    // Count query (isCount: true — used by FilterProvider for pagination meta)
    // -----------------------------------------------------------------------

    [Fact]
    public void Should_NotContainLimit_WhenCountQueryRequested()
    {
        var result = _sut.BuildSqlQuery<TestEntity>(
            Build("name=eq.Alice", ignorePaging: true));

        // When BuildSqlQuery processes a count-only tokenisation the data builder
        // is null (ShowCount only) or the SQL has no LIMIT.
        // Either way LIMIT must not appear.
        var sql = result.Builder?.AsSql().Sql ?? string.Empty;
        sql.Should().NotContain("LIMIT");
    }

    // -----------------------------------------------------------------------
    // Meta
    // -----------------------------------------------------------------------

    [Fact]
    public void Should_HaveShowCountFalse_WhenNoCountTokenInQuery()
    {
        // ShowCount is only true when the query explicitly includes a count token.
        // A plain filter query does not emit a count token, so ShowCount = false.
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
        sql.Should().Contain("ILIKE");

        var p = result.Builder.Build().SqlParameters;
        p[0].Argument.Should().Be("alice%");
    }

    [Fact]
    public void Should_GenerateEndsWithPattern_WhenEnwOperatorUsed()
    {
        var result = _sut.BuildSqlQuery<TestEntity>(Build("name=enw.alice"));

        var sql = result.Builder!.AsSql().Sql;
        sql.Should().Contain("ILIKE");

        var p = result.Builder.Build().SqlParameters;
        p[0].Argument.Should().Be("%alice");
    }

    [Fact]
    public void Should_GenerateLikeClause_WhenLikeOperatorUsed()
    {
        // like operator uses ILIKE without adding wildcards — the caller supplies the pattern
        var result = _sut.BuildSqlQuery<TestEntity>(Build("name=like.alice"));

        var sql = result.Builder!.AsSql().Sql;
        sql.Should().Contain("ILIKE");

        var p = result.Builder.Build().SqlParameters;
        p[0].Argument.Should().Be("alice");
    }

    // -----------------------------------------------------------------------
    // Is operator
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
        // is.$empty on a non-text field → only NULL check (no empty-string check)
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
    public void Should_GenerateIsTrue_WhenIsTrueOnBoolField()
    {
        // PostgreSQL: field IS <bool literal>
        var result = _sut.BuildSqlQuery<TestEntity>(Build("isActive=is.true"));

        var sql = result.Builder!.AsSql().Sql;
        sql.Should().Contain("isActive");
        sql.Should().Contain("IS");
        sql.Should().Contain("True");
    }

    [Fact]
    public void Should_GenerateIsFalse_WhenIsFalseOnBoolField()
    {
        var result = _sut.BuildSqlQuery<TestEntity>(Build("isActive=is.false"));

        var sql = result.Builder!.AsSql().Sql;
        sql.Should().Contain("isActive");
        sql.Should().Contain("IS");
        sql.Should().Contain("False");
    }

    [Fact]
    public void Should_GenerateIsNotTrue_WhenNegatedIsTrueOnBoolField()
    {
        // is.not.true → field IS NOT True
        var result = _sut.BuildSqlQuery<TestEntity>(Build("isActive=is.not.true"));

        var sql = result.Builder!.AsSql().Sql;
        sql.Should().Contain("isActive");
        sql.Should().Contain("IS");
        sql.Should().Contain("not");
        sql.Should().Contain("True");
    }

    // -----------------------------------------------------------------------
    // Negated operators
    // -----------------------------------------------------------------------

    [Fact]
    public void Should_WrapInNullCheck_WhenNotEqualityOnTextField()
    {
        // not.eq on text → NOT ILIKE wrapped with OR IS NULL
        var result = _sut.BuildSqlQuery<TestEntity>(Build("name=not.eq.Alice"));

        var sql = result.Builder!.AsSql().Sql;
        sql.Should().Contain("NOT ILIKE");
        sql.Should().Contain("IS NULL");
    }

    [Fact]
    public void Should_WrapInNullCheck_WhenNotContainsOnTextField()
    {
        // not.cs on text → NOT ILIKE with wildcard, wrapped with OR IS NULL
        var result = _sut.BuildSqlQuery<TestEntity>(Build("name=not.cs.test"));

        var sql = result.Builder!.AsSql().Sql;
        sql.Should().Contain("NOT ILIKE");
        sql.Should().Contain("IS NULL");

        var p = result.Builder.Build().SqlParameters;
        p[0].Argument.Should().Be("%test%");
    }

    [Fact]
    public void Should_GenerateNegatedAnyArrayClause_WhenNotInOperatorOnStringField()
    {
        // not.in on text → NOT (col ILIKE ANY(ARRAY[...])) wrapped with OR IS NULL
        var result = _sut.BuildSqlQuery<TestEntity>(Build("name=not.in.(alice,bob)"));

        var sql = result.Builder!.AsSql().Sql;
        sql.Should().Contain("NOT");
        sql.Should().Contain("ANY(ARRAY[");
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
        sql.Should().Contain("ILIKE");
        sql.Should().Contain(">");
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
