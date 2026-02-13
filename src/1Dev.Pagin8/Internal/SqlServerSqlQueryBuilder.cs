using _1Dev.Pagin8.Internal.Tokenizer.Contracts;

namespace _1Dev.Pagin8.Internal;

/// <summary>
/// SQL Server-specific query builder.
/// This is a wrapper around SqlQueryBuilder with a marker interface.
/// </summary>
public class SqlServerSqlQueryBuilder(ITokenizationService tokenizationService, ISqlTokenVisitor tokenVisitor)
    : SqlQueryBuilder(tokenizationService, tokenVisitor), ISqlServerSqlQueryBuilder;
