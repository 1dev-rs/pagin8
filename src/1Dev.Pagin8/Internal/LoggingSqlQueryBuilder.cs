using _1Dev.Pagin8.Input;
using Microsoft.Extensions.Logging;

namespace _1Dev.Pagin8.Internal;

internal sealed partial class LoggingSqlQueryBuilder(
    ISqlQueryBuilder inner,
    ILoggerFactory loggerFactory) : ISqlQueryBuilder
{
    private readonly ILogger logger = loggerFactory.CreateLogger("Pagin8");

    public QueryBuilderResult BuildSqlQuery<T>(QueryBuilderParameters parameters) where T : class
    {
        var result = inner.BuildSqlQuery<T>(parameters);

        if (!logger.IsEnabled(LogLevel.Trace)) return result;

        if (result.Builder is null)
        {
            LogCountOnlyQuery();
            return result;
        }

        var sql = result.Builder.AsSql();
        LogGeneratedSql(sql.Sql);

        var formattedParams = string.Join(", ", sql.SqlParameters.Select((p, i) => $"@p{i}={p.Argument}"));
        if (formattedParams.Length > 0)
            LogSqlParameters(formattedParams);

        return result;
    }

    [LoggerMessage(EventId = 1001, Level = LogLevel.Trace, Message = "Pagin8 generated SQL: {Sql}")]
    private partial void LogGeneratedSql(string sql);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Trace, Message = "Pagin8 SQL parameters: {Parameters}")]
    private partial void LogSqlParameters(string parameters);

    [LoggerMessage(EventId = 1003, Level = LogLevel.Trace, Message = "Pagin8 count-only query (no SQL generated)")]
    private partial void LogCountOnlyQuery();
}
