using Microsoft.Extensions.Logging;

namespace _1Dev.Pagin8.Test.SqlQueryBuilderTests.Internal;

internal sealed class TestLoggerProvider(LogLevel minLevel = LogLevel.Trace) : ILoggerProvider
{
    public List<LogEntry> Entries { get; } = [];

    public ILogger CreateLogger(string categoryName) => new TestLogger(this, categoryName, minLevel);
    public void Dispose() { }
}

internal sealed class TestLogger(TestLoggerProvider provider, string category, LogLevel minLevel) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => logLevel >= minLevel;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;
        provider.Entries.Add(new LogEntry(category, logLevel, eventId, formatter(state, exception)));
    }
}

internal sealed record LogEntry(string Category, LogLevel Level, EventId EventId, string Message);
