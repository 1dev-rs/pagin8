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

public class SqlServerQueryBuilderTests
{
    private ISqlQueryBuilder CreateSqlServerQueryBuilder()
    {
        Pagin8Runtime.Initialize(new ServiceConfiguration
        {
            MaxNestingLevel = 5,
            PagingSettings = new PagingSettings
            {
                DefaultPerPage = 50,
                MaxItemsPerPage = 5000,
                MaxSafeItemCount = 1_000_000
            },
            DatabaseType = DatabaseType.SqlServer
        });

        var tokenizer = new Tokenizer();
        var contextValidator = new PassThroughContextValidator();
        var metadataProvider = new Pagin8MetadataProvider(new MetadataProvider());
        var dateProcessor = new DateProcessor();

        var tokenizationService = new TokenizationService(tokenizer, contextValidator, metadataProvider);
        var tokenVisitor = new SqlServerTokenVisitor(metadataProvider, dateProcessor);

        return new SqlQueryBuilder(tokenizationService, tokenVisitor);
    }

    [Fact]
    public void BuildSqlQuery_ShouldGenerateExpectedSql_ForSimpleEquality()
    {
        var _sut = CreateSqlServerQueryBuilder();

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
        var _sut = CreateSqlServerQueryBuilder();

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
        var _sut = CreateSqlServerQueryBuilder();

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
        var _sut = CreateSqlServerQueryBuilder();

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
        var _sut = CreateSqlServerQueryBuilder();

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
        var _sut = CreateSqlServerQueryBuilder();

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
        var _sut = CreateSqlServerQueryBuilder();

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
        var _sut = CreateSqlServerQueryBuilder();

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
        var _sut = CreateSqlServerQueryBuilder();

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
        var _sut = CreateSqlServerQueryBuilder();

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
        var _sut = CreateSqlServerQueryBuilder();

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
}
