namespace _1Dev.Pagin8;

/// <summary>
/// Marker interface for the DuckDB query builder.
/// Used to inject the DuckDB-specific query builder with DuckDbTokenVisitor.
/// </summary>
public interface IDuckDbSqlQueryBuilder : ISqlQueryBuilder
{
}
