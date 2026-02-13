using _1Dev.Pagin8.Extensions.Backend.Interfaces;

namespace _1Dev.Pagin8.Extensions.Backend.Implementations;

/// <summary>
/// Filter provider for SQL Server database.
/// This is just a wrapper around FilterProvider with a marker interface.
/// Uses SQL Server-specific query builder with SqlServerTokenVisitor.
/// </summary>
public class SqlServerFilterProvider(
    ISqlServerDbConnectionFactory connectionFactory,
    ISqlServerSqlQueryBuilder sqlQueryBuilder)
    : FilterProvider(connectionFactory, sqlQueryBuilder), ISqlServerFilterProvider;
