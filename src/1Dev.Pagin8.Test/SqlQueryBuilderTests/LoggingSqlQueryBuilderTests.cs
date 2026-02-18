using _1Dev.Pagin8.Input;
using _1Dev.Pagin8.Internal;
using _1Dev.Pagin8.Internal.DateProcessor;
using _1Dev.Pagin8.Internal.Metadata;
using _1Dev.Pagin8.Internal.Metadata.Models;
using _1Dev.Pagin8.Internal.Tokenizer;
using _1Dev.Pagin8.Internal.Visitors;
using _1Dev.Pagin8.Test.SqlQueryBuilderTests.Internal;
using FluentAssertions;
using Microsoft.Extensions.Logging;

namespace _1Dev.Pagin8.Test.SqlQueryBuilderTests;

public class LoggingSqlQueryBuilderTests
{
    private readonly ISqlQueryBuilder _inner;

    public LoggingSqlQueryBuilderTests()
    {
        Pagin8TestBootstrap.Init();
        var tokenizer = new Tokenizer();
        var contextValidator = new PassThroughContextValidator();
        var metadataProvider = new Pagin8MetadataProvider(new MetadataProvider());
        var dateProcessor = new DateProcessor();
        var tokenizationService = new TokenizationService(tokenizer, contextValidator, metadataProvider);
        var tokenVisitor = new NpgsqlTokenVisitor(metadataProvider, dateProcessor);

        _inner = new SqlQueryBuilder(tokenizationService, tokenVisitor);
    }

    private static QueryBuilderParameters CreateParameters(string queryString = "name=eq.John") => new()
    {
        InputParameters = QueryInputParameters.Create(
            sql: "SELECT * FROM test_entity",
            queryString: queryString, string.Empty, true
        )
    };

    [Fact]
    public void Should_Log_Sql_And_Parameters_When_Trace_Enabled()
    {
        var loggerFactory = new TestLoggerFactory(LogLevel.Trace);
        var sut = new LoggingSqlQueryBuilder(_inner, loggerFactory);

        sut.BuildSqlQuery<TestEntity>(CreateParameters());

        loggerFactory.Entries.Should().Contain(e =>
            e.Category == "Pagin8" &&
            e.Level == LogLevel.Trace &&
            e.EventId.Id == 1001 &&
            e.Message.Contains("ILIKE"));

        loggerFactory.Entries.Should().Contain(e =>
            e.Category == "Pagin8" &&
            e.Level == LogLevel.Trace &&
            e.EventId.Id == 1002 &&
            e.Message.Contains("@p0=john"));
    }

    [Fact]
    public void Should_Not_Log_When_Trace_Disabled()
    {
        var loggerFactory = new TestLoggerFactory(LogLevel.Information);
        var sut = new LoggingSqlQueryBuilder(_inner, loggerFactory);

        sut.BuildSqlQuery<TestEntity>(CreateParameters());

        loggerFactory.Entries.Should().BeEmpty();
    }

    [Fact]
    public void Should_Return_Same_Result_As_Inner_Builder()
    {
        var loggerFactory = new TestLoggerFactory(LogLevel.Trace);
        var sut = new LoggingSqlQueryBuilder(_inner, loggerFactory);
        var parameters = CreateParameters();

        var decoratedResult = sut.BuildSqlQuery<TestEntity>(parameters);
        var directResult = _inner.BuildSqlQuery<TestEntity>(parameters);

        decoratedResult.Builder!.AsSql().Sql.Should().Be(directResult.Builder!.AsSql().Sql);
    }
}
