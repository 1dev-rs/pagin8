using _1Dev.Pagin8.Extensions.Backend.Interfaces;

namespace _1Dev.Pagin8.Extensions.Backend.Implementations;

/// <summary>
/// Filter provider for DuckDB.
/// This is just a wrapper around FilterProvider with a marker interface.
/// Uses the DuckDB-specific query builder with DuckDbTokenVisitor.
/// </summary>
public class DuckDbFilterProvider(
    IDuckDbConnectionFactory connectionFactory,
    IDuckDbSqlQueryBuilder sqlQueryBuilder)
    : FilterProvider(connectionFactory, sqlQueryBuilder, connectionFactory.CommandTimeout), IDuckDbFilterProvider;
