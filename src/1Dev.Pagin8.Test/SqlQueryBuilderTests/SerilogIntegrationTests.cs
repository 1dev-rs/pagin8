using _1Dev.Pagin8.Extensions;
using _1Dev.Pagin8.Input;
using _1Dev.Pagin8.Internal;
using _1Dev.Pagin8.Test.SqlQueryBuilderTests.Internal;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace _1Dev.Pagin8.Test.SqlQueryBuilderTests;

public class SerilogIntegrationTests
{
    private static QueryBuilderParameters CreateParameters(string queryString = "name=eq.John") => new()
    {
        InputParameters = QueryInputParameters.Create(
            sql: "SELECT * FROM test_entity",
            queryString: queryString, string.Empty, true
        )
    };

    [Fact]
    public void Should_Log_To_Serilog_When_Pagin8_Override_Is_Verbose()
    {
        var sink = new InMemorySink();

        var serilogLogger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Pagin8", LogEventLevel.Verbose)
            .WriteTo.Sink(sink)
            .CreateLogger();

        var services = new ServiceCollection();
        services.AddPagin8();
        services.AddLogging(builder => builder.AddSerilog(serilogLogger, dispose: true));

        var sp = services.BuildServiceProvider();
        var sut = sp.GetRequiredService<ISqlQueryBuilder>();

        sut.BuildSqlQuery<TestEntity>(CreateParameters());

        var pagin8Message = sink.Events
            .Where(e =>
                e.Properties.TryGetValue("SourceContext", out var ctx) &&
                ctx.ToString().Contains("Pagin8") &&
                e.Level == LogEventLevel.Verbose)
            .Select(e => e.RenderMessage())
            .Single();

        pagin8Message.Should().Contain("TestEntity");
        pagin8Message.Should().Contain("name=eq.John");
        pagin8Message.Should().Contain("ILIKE");
    }

    [Fact]
    public void Should_Not_Log_When_Pagin8_Override_Is_Not_Set()
    {
        var sink = new InMemorySink();

        var serilogLogger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Sink(sink)
            .CreateLogger();

        var services = new ServiceCollection();
        services.AddPagin8();
        services.AddLogging(builder => builder.AddSerilog(serilogLogger, dispose: true));

        var sp = services.BuildServiceProvider();
        var sut = sp.GetRequiredService<ISqlQueryBuilder>();

        sut.BuildSqlQuery<TestEntity>(CreateParameters());

        sink.Events.Where(e =>
            e.Properties.TryGetValue("SourceContext", out var ctx) &&
            ctx.ToString().Contains("Pagin8")).Should().BeEmpty();
    }
}

internal sealed class InMemorySink : ILogEventSink
{
    public List<LogEvent> Events { get; } = [];
    public void Emit(LogEvent logEvent) => Events.Add(logEvent);
}
