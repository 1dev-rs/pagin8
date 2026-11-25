using _1Dev.Pagin8.Internal.Tokenizer.Contracts;

namespace _1Dev.Pagin8.Internal;

/// <summary>
/// DuckDB-specific query builder.
/// This is a wrapper around SqlQueryBuilder with a marker interface.
/// </summary>
public class DuckDbSqlQueryBuilder(ITokenizationService tokenizationService, ISqlTokenVisitor tokenVisitor)
    : SqlQueryBuilder(tokenizationService, tokenVisitor), IDuckDbSqlQueryBuilder;
