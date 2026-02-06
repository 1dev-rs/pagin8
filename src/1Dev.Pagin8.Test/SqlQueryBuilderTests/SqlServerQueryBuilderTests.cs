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

namespace _1Dev.Pagin8.Test.SqlQueryBuilderTests;

[Collection("SQL Server QueryBuilder Tests")]
public class SqlServerQueryBuilderTests
{
    private readonly ISqlQueryBuilder _sut;

    public SqlServerQueryBuilderTests()
    {
        // Ensure SQL Server configuration for these tests
        // This is needed because PostgreSQL tests may run first and set different configuration
        SqlServerTestBootstrap.Init();

        var tokenizer = new Tokenizer();
        var contextValidator = new PassThroughContextValidator();
        var metadataProvider = new Pagin8MetadataProvider(new MetadataProvider());
        var dateProcessor = new DateProcessor();

        var tokenizationService = new TokenizationService(tokenizer, contextValidator, metadataProvider);
        var tokenVisitor = new SqlServerTokenVisitor(metadataProvider, dateProcessor);

        _sut = new SqlQueryBuilder(tokenizationService, tokenVisitor);
    }

    [Fact]
    public void BuildSqlQuery_ShouldGenerateExpectedSql_ForSimpleEquality()
    {
        var parameters = new QueryBuilderParameters
        {
            InputParameters = QueryInputParameters.Create(
                sql: "SELECT * FROM test_entity",
                queryString: "name=eq.John", string.Empty, true
            )
        };

        var result = _sut.BuildSqlQuery<TestEntity>(parameters);

        var sql = result.Builder.AsSql().Sql;
        var @params = result.Builder.Build().SqlParameters;

        sql.Should().Be(
            "AND name LIKE @p0 ORDER BY id ASC OFFSET 0 ROWS FETCH NEXT @p1 ROWS ONLY"
        );

        @params.Should().HaveCount(2);

        @params[0].Argument.Should().Be("john");
        @params[1].Argument.Should().Be(1_000_000);
    }

    [Fact]
    public void BuildSqlQuery_ShouldGenerateExpectedSql_ForNestedObjectComparison()
    {
        var parameters = new QueryBuilderParameters
        {
            InputParameters = QueryInputParameters.Create(
                sql: "SELECT * FROM test_nested_entity",
                queryString: "testEntity.with=(name.stw.test)", string.Empty, true
            )
        };

        var result = _sut.BuildSqlQuery<TestNestedEntity>(parameters);

        var sql = result.Builder.AsSql().Sql;
        var @params = result.Builder.Build().SqlParameters;

        sql.Should().Be(
            "AND (JSON_VALUE(testEntity, '$.name') LIKE @p0 ESCAPE '\\' ) ORDER BY id ASC OFFSET 0 ROWS FETCH NEXT @p1 ROWS ONLY"
        );

        @params.Should().HaveCount(2);

        @params[0].Argument.Should().Be("test%");
        @params[1].Argument.Should().Be(1_000_000);
    }

    [Fact]
    public void BuildSqlQuery_ShouldGenerateExpectedSql_ForChainedNestedObjectComparison()
    {
        var parameters = new QueryBuilderParameters
        {
            InputParameters = QueryInputParameters.Create(
                sql: "SELECT * FROM test_nested_entity",
                queryString: "testEntity.with=(name.stw.test,amount.gt.500)", string.Empty, true
            )
        };

        var result = _sut.BuildSqlQuery<TestNestedEntity>(parameters);

        var sql = result.Builder.AsSql().Sql;
        var @params = result.Builder.Build().SqlParameters;

        sql.Should().Be(
            "AND (JSON_VALUE(testEntity, '$.name') LIKE @p0 ESCAPE '\\'  AND JSON_VALUE(testEntity, '$.amount') > @p1 ) ORDER BY id ASC OFFSET 0 ROWS FETCH NEXT @p2 ROWS ONLY"
        );

        @params.Should().HaveCount(3);

        @params[0].Argument.Should().Be("test%");
        @params[1].Argument.Should().Be(500m);
        @params[2].Argument.Should().Be(1_000_000);
    }

    [Fact]
    public void BuildSqlQuery_ShouldGenerateExpectedSql_ForGroupDsl()
    {
        var parameters = new QueryBuilderParameters
        {
            InputParameters = QueryInputParameters.Create(
                sql: "SELECT * FROM test_entity",
                queryString: "or=(name.cs.test space, id.gt.1)", string.Empty, true
            )
        };

        var result = _sut.BuildSqlQuery<TestEntity>(parameters);

        var sql = result.Builder.AsSql().Sql;
        var @params = result.Builder.Build().SqlParameters;

        sql.Should().Be(
            "AND (name LIKE @p0 ESCAPE '\\' OR id > @p1 ) ORDER BY id ASC OFFSET 0 ROWS FETCH NEXT @p2 ROWS ONLY"
        );

        @params.Should().HaveCount(3);

        @params[0].Argument.Should().Be("%test space%");
        @params[1].Argument.Should().Be(1);
        @params[2].Argument.Should().Be(1_000_000);
    }

    [Fact]
    public void BuildSqlQuery_ShouldGenerateExpectedSql_ForInOperator()
    {
        var parameters = new QueryBuilderParameters
        {
            InputParameters = QueryInputParameters.Create(
                sql: "SELECT * FROM test_entity",
                queryString: "id=in.(1,2,3)", string.Empty, true
            )
        };

        var result = _sut.BuildSqlQuery<TestEntity>(parameters);

        var sql = result.Builder.AsSql().Sql;
        var @params = result.Builder.Build().SqlParameters;

        sql.Should().Be(
            "AND id IN (1,2,3) ORDER BY id ASC OFFSET 0 ROWS FETCH NEXT @p0 ROWS ONLY"
        );

        @params.Should().HaveCount(1);
        @params[0].Argument.Should().Be(1_000_000);
    }

    [Fact]
    public void BuildSqlQuery_ShouldHaveOrderBy_WhenNoSortSpecified()
    {
        var parameters = new QueryBuilderParameters
        {
            InputParameters = QueryInputParameters.Create(
                sql: "SELECT * FROM test_entity",
                queryString: "name=eq.test", string.Empty, true
            )
        };

        var result = _sut.BuildSqlQuery<TestEntity>(parameters);

        var sql = result.Builder.AsSql().Sql;

        sql.Should().Contain("ORDER BY");
        sql.Should().Contain("id ASC");
    }

    [Fact]
    public void BuildSqlQuery_ShouldHandleSimpleQuery()
    {
        var parameters = new QueryBuilderParameters
        {
            InputParameters = QueryInputParameters.Create(
                sql: "SELECT * FROM test_entity",
                queryString: "id=gt.100", string.Empty, true
            )
        };

        var result = _sut.BuildSqlQuery<TestEntity>(parameters);

        result.Builder.Should().NotBeNull();
        var sql = result.Builder.AsSql().Sql;
        sql.Should().Contain("id >");
    }

    [Fact]
    public void BuildSqlQuery_ShouldHandleDateComparisons()
    {
        var parameters = new QueryBuilderParameters
        {
            InputParameters = QueryInputParameters.Create(
                sql: "SELECT * FROM test_entity",
                queryString: "createdDate=eq.2024-01-01", string.Empty, true
            )
        };

        var result = _sut.BuildSqlQuery<TestEntity>(parameters);

        var sql = result.Builder.AsSql().Sql;

        sql.Should().Contain("CAST");
        sql.Should().Contain("DATE");
    }

    [Fact]
    public void BuildSqlQuery_ShouldUseFetchNext_WithSimpleQuery()
    {
        var parameters = new QueryBuilderParameters
        {
            InputParameters = QueryInputParameters.Create(
                sql: "SELECT * FROM test_entity",
                queryString: "id=gt.100", string.Empty, true
            )
        };

        var result = _sut.BuildSqlQuery<TestEntity>(parameters);

        var sql = result.Builder.AsSql().Sql;

        sql.Should().Contain("OFFSET 0 ROWS FETCH NEXT");
        sql.Should().Contain("ROWS ONLY");
        sql.Should().NotContain("LIMIT");
    }

    [Fact]
    public void BuildSqlQuery_ShouldHandleSpecialCharactersInLike()
    {
        var parameters = new QueryBuilderParameters
        {
            InputParameters = QueryInputParameters.Create(
                sql: "SELECT * FROM test_entity",
                queryString: "name=cs.test_bracket", string.Empty, true
            )
        };

        var result = _sut.BuildSqlQuery<TestEntity>(parameters);

        var sql = result.Builder.AsSql().Sql;

        sql.Should().Contain("ESCAPE '\\'");
    }

    [Fact]
    public void BuildSqlQuery_ShouldHandleNegation()
    {
        var parameters = new QueryBuilderParameters
        {
            InputParameters = QueryInputParameters.Create(
                sql: "SELECT * FROM test_entity",
                queryString: "name=not.eq.John", string.Empty, true
            )
        };

        var result = _sut.BuildSqlQuery<TestEntity>(parameters);

        var sql = result.Builder.AsSql().Sql;

        sql.Should().Contain("IS NULL");
    }

    [Fact]
    public void BuildSqlQuery_ShouldGenerateExpectedSql_ForCustomDsl()
    {
        var parameters = new QueryBuilderParameters
        {
            InputParameters = QueryInputParameters.Create(
                sql: "SELECT * FROM test_entity",
                queryString: "and=%28name.cs.%28karate+klub%29%29", string.Empty, true
            )
        };

        var result = _sut.BuildSqlQuery<TestEntity>(parameters);

        var sql = result.Builder.AsSql().Sql;
        var @params = result.Builder.Build().SqlParameters;

        sql.Should().Be(
            "AND (name LIKE @p0 ESCAPE '\\' ) ORDER BY id ASC OFFSET 0 ROWS FETCH NEXT @p1 ROWS ONLY"
        );

        @params[0].Argument.Should().Be("%karate klub%");
    }

    [Fact]
    public void BuildSqlQuery_ShouldGenerateExpectedSql_ForCustomDslWithTrailingComma()
    {
        var parameters = new QueryBuilderParameters
        {
            InputParameters = QueryInputParameters.Create(
                sql: "SELECT * FROM test_entity",
                queryString: "and=%28name.stw.in.%28karate,%29%29", string.Empty, true
            )
        };

        var result = _sut.BuildSqlQuery<TestEntity>(parameters);

        var sql = result.Builder.AsSql().Sql;
        var @params = result.Builder.Build().SqlParameters;

        sql.Should().Be(
            "AND (name IN ('karate%')) ORDER BY id ASC OFFSET 0 ROWS FETCH NEXT @p0 ROWS ONLY"
        );
    }

    [Theory]
    [InlineData("2025-12-11", 2025, 12, 11, 0, 0, 0, 0)]
    [InlineData("2025-12-11T14:30:00", 2025, 12, 11, 14, 30, 0, 0)]
    [InlineData("2025-12-11T14:30:00.123", 2025, 12, 11, 14, 30, 0, 123)]
    public void BuildSqlQuery_ShouldAccept_AllowedDateFormats(
        string input,
        int y, int m, int d, int h, int min, int s, int ms)
    {
        var parameters = new QueryBuilderParameters
        {
            InputParameters = QueryInputParameters.Create(
                sql: "SELECT * FROM test_entity",
                queryString: $"modifiedDate=gt.{input}",
                string.Empty,
                true
            )
        };

        var result = _sut.BuildSqlQuery<TestEntity>(parameters);
        var sqlParams = result.Builder.Build().SqlParameters;

        sqlParams.Should().HaveCount(2);
        sqlParams[0].Argument.Should().BeOfType<DateTime>();

        sqlParams[0].Argument.Should().Be(
            new DateTime(y, m, d, h, min, s, ms)
        );
    }

    [Theory]
    [InlineData("11.12.2025")]
    [InlineData("12/11/2025")]
    [InlineData("Mon,+08+Dec+2025+23:00:00+GMT")]
    [InlineData("08 Dec 2025 23:00:00")]
    [InlineData("2025/12/11")]
    public void BuildSqlQuery_ShouldReject_DisallowedDateFormats(string input)
    {
        var parameters = new QueryBuilderParameters
        {
            InputParameters = QueryInputParameters.Create(
                sql: "SELECT * FROM test_entity",
                queryString: $"modifiedDate=gt.{input}",
                string.Empty,
                true
            )
        };

        Action act = () => _sut.BuildSqlQuery<TestEntity>(parameters);

        act.Should().Throw<ArgumentException>();
    }
}
