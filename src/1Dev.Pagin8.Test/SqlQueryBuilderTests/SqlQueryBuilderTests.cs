using _1Dev.Pagin8.Input;
using _1Dev.Pagin8.Internal;
using _1Dev.Pagin8.Internal.DateProcessor;
using _1Dev.Pagin8.Internal.Metadata;
using _1Dev.Pagin8.Internal.Metadata.Models;
using _1Dev.Pagin8.Internal.Tokenizer;
using _1Dev.Pagin8.Internal.Visitors;
using _1Dev.Pagin8.Test.SqlQueryBuilderTests.Internal;
using FluentAssertions;

namespace _1Dev.Pagin8.Test.SqlQueryBuilderTests;

public class SqlQueryBuilderTests
{
    private readonly ISqlQueryBuilder _sut;

    public SqlQueryBuilderTests()
    {
        Pagin8TestBootstrap.Init();
        var tokenizer = new Tokenizer(); 
        var contextValidator = new PassThroughContextValidator(); 
        var metadataProvider = new Pagin8MetadataProvider(new MetadataProvider());
        var dateProcessor = new DateProcessor();

        var tokenizationService = new TokenizationService(tokenizer, contextValidator, metadataProvider);
        var tokenVisitor = new NpgsqlTokenVisitor(metadataProvider, dateProcessor);

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
            "AND name ILIKE @p0 ORDER BY id ASC LIMIT @p1"
        );

        @params.Should().HaveCount(2);

        @params[0].Argument.Should().Be("john");
        @params[1].Argument.Should().Be(1_000_000);
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
            "AND (name ILIKE @p0 ESCAPE '\\' OR id > @p1 ) ORDER BY id ASC LIMIT @p2"
        );

        @params.Should().HaveCount(3);

        @params[0].Argument.Should().Be("%test space%");
        @params[1].Argument.Should().Be(1);
        @params[2].Argument.Should().Be(1_000_000);
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
            "AND (name ILIKE @p0 ESCAPE '\\' ) ORDER BY id ASC LIMIT @p1"
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
            "AND ((name ILIKE 'karate%')) ORDER BY id ASC LIMIT @p0"
        );
    }
}