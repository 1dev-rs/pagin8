using _1Dev.Pagin8.Extensions;
using _1Dev.Pagin8.Input;
using _1Dev.Pagin8.Internal;
using _1Dev.Pagin8.Test.SqlQueryBuilderTests.Internal;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace _1Dev.Pagin8.Test.SqlQueryBuilderTests;

public class LoggingSqlQueryBuilderTests
{
    private static QueryBuilderParameters CreateParameters(string queryString = "name=eq.John") => new()
    {
        InputParameters = QueryInputParameters.Create(
            sql: "SELECT * FROM test_entity",
            queryString: queryString, string.Empty, true
        )
    };

    private static (ISqlQueryBuilder sut, TestLoggerProvider loggerProvider) CreateSut(LogLevel minLevel)
    {
        var loggerProvider = new TestLoggerProvider(minLevel);

        var services = new ServiceCollection();
        services.AddPagin8();
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.SetMinimumLevel(minLevel);
            builder.AddProvider(loggerProvider);
        });

        var sp = services.BuildServiceProvider();
        return (sp.GetRequiredService<ISqlQueryBuilder>(), loggerProvider);
    }

    [Fact]
    public void Should_Log_Query_With_Entity_And_Params_When_Trace_Enabled()
    {
        var (sut, loggerProvider) = CreateSut(LogLevel.Trace);

        sut.BuildSqlQuery<TestEntity>(CreateParameters());

        var entry = loggerProvider.Entries.Single(e =>
            e.Category == "Pagin8" && e.EventId.Id == 1001);

        entry.Level.Should().Be(LogLevel.Trace);
        entry.Message.Should().Contain("TestEntity");
        entry.Message.Should().Contain("name=eq.John");
        entry.Message.Should().Contain("ILIKE");
        entry.Message.Should().Contain("@p0 (String) = 'john'");
    }

    [Fact]
    public void Should_Not_Log_When_Trace_Disabled()
    {
        var (sut, loggerProvider) = CreateSut(LogLevel.Information);

        sut.BuildSqlQuery<TestEntity>(CreateParameters());

        loggerProvider.Entries.Where(e => e.Category == "Pagin8").Should().BeEmpty();
    }

    [Fact]
    public void Should_Return_Valid_Query_Result()
    {
        var (sut, _) = CreateSut(LogLevel.Trace);

        var result = sut.BuildSqlQuery<TestEntity>(CreateParameters());

        result.Builder.Should().NotBeNull();
        result.Builder!.AsSql().Sql.Should().Contain("ILIKE");
    }
}
