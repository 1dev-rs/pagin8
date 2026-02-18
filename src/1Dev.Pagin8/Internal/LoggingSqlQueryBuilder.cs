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

        var entityName = typeof(T).Name;
        var queryString = parameters.InputParameters.QueryString;

        if (result.Builder is null)
        {
            LogCountOnlyQuery(entityName, queryString);
            return result;
        }

        var sql = result.Builder.AsSql();
        var sqlParams = sql.SqlParameters;

        var formattedParams = string.Join(", ",
            sqlParams.Select((p, i) => $"@p{i} ({p.Argument?.GetType().Name ?? "null"}) = '{p.Argument}'"));

        LogQueryBuilt(entityName, queryString, sql.Sql, sqlParams.Count, formattedParams);

        return result;
    }

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Trace,
        Message = "Query built for {EntityName} | Filter: \"{QueryString}\" | SQL: {Sql} | Params ({ParameterCount}): [{Parameters}]")]
    private partial void LogQueryBuilt(string entityName, string queryString, string sql, int parameterCount, string parameters);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Trace,
        Message = "Count-only query for {EntityName} | Filter: \"{QueryString}\" | No SQL generated")]
    private partial void LogCountOnlyQuery(string entityName, string queryString);
}
