using Microsoft.Extensions.Logging;

namespace _1Dev.Pagin8.Internal;

public sealed class LoggingDuckDbSqlQueryBuilder(
    IDuckDbSqlQueryBuilder inner,
    ILoggerFactory loggerFactory) : LoggingSqlQueryBuilder(inner, loggerFactory), IDuckDbSqlQueryBuilder;
