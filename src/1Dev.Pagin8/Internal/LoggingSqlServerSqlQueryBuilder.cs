using Microsoft.Extensions.Logging;

namespace _1Dev.Pagin8.Internal;

public sealed class LoggingSqlServerSqlQueryBuilder(
    ISqlServerSqlQueryBuilder inner,
    ILoggerFactory loggerFactory) : LoggingSqlQueryBuilder(inner, loggerFactory), ISqlServerSqlQueryBuilder;
