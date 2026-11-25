using _1Dev.Pagin8.Internal.DateProcessor;
using _1Dev.Pagin8.Internal.Metadata.Models;
using _1Dev.Pagin8.Internal.Tokenizer.Contracts;
using _1Dev.Pagin8.Internal.Tokenizer.Operators;
using _1Dev.Pagin8.Internal.Tokenizer.Tokens;
using _1Dev.Pagin8.Internal.Tokenizer.Tokens.Sort;

namespace _1Dev.Pagin8.Internal.Visitors;

/// <summary>
/// DuckDB SQL emission. DuckDB accepts the PostgreSQL dialect pagin8 emits for flat models
/// (ILIKE, ESCAPE, DATE(), = ANY(ARRAY[...]), ::casts, LIMIT, the keyset OR chain), so every
/// token is delegated to <see cref="NpgsqlTokenVisitor"/>. The parameter marker differs
/// (@p0 against DuckDB's $p0) and is rewritten when the command runs, by the Backend's
/// DuckDB connection. A DuckDB-specific difference belongs in the matching method below.
/// </summary>
public class DuckDbTokenVisitor(IPagin8MetadataProvider metadata, IDateProcessor dateProcessor) : ISqlTokenVisitor
{
    private readonly NpgsqlTokenVisitor _postgres = new(metadata, dateProcessor);

    public QueryBuilderResult Visit<T>(ComparisonToken token, QueryBuilderResult builder) where T : class
        => _postgres.Visit<T>(token, builder);

    public QueryBuilderResult Visit<T>(GroupToken token, QueryBuilderResult builder) where T : class
        => _postgres.Visit<T>(token, builder);

    // PostgreSQL's `col ILIKE ANY(ARRAY[...])` for text is refused by DuckDB ("Unsupported comparison
    // ~~* for ANY/ALL"), so a text IN becomes the same values as eq comparisons: OR-ed, or for
    // not.in AND-ed negations, each of which keeps NULL rows exactly like the PostgreSQL form does.
    public QueryBuilderResult Visit<T>(InToken token, QueryBuilderResult builder) where T : class
    {
        if (!string.IsNullOrEmpty(token.JsonPath) || metadata.GetTypeCodeForProperty<T>(token.Field) != TypeCode.String)
            return _postgres.Visit<T>(token, builder);

        var comparisons = token.Values
            .Trim('(', ')')
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(v => v.Trim('\'', ' '))
            .Select(v => (Token)new ComparisonToken(token.Field, token.Comparison, v, token.NestingLevel + 1, token.IsNegated))
            .ToList();

        if (comparisons.Count == 0)
            return _postgres.Visit<T>(token, builder);

        var group = new GroupToken(token.IsNegated ? NestingOperator.And : NestingOperator.Or, comparisons, token.NestingLevel);
        return _postgres.Visit<T>(group, builder);
    }

    public QueryBuilderResult Visit<T>(SortToken token, QueryBuilderResult builder) where T : class
        => _postgres.Visit<T>(token, builder);

    public QueryBuilderResult Visit<T>(LimitToken token, QueryBuilderResult builder) where T : class
        => _postgres.Visit<T>(token, builder);

    public QueryBuilderResult Visit<T>(SelectToken token, QueryBuilderResult builder) where T : class
        => _postgres.Visit<T>(token, builder);

    public QueryBuilderResult Visit<T>(PagingToken token, QueryBuilderResult builder) where T : class
        => _postgres.Visit<T>(token, builder);

    public QueryBuilderResult Visit<T>(ShowCountToken token, QueryBuilderResult builder) where T : class
        => _postgres.Visit<T>(token, builder);

    public QueryBuilderResult Visit<T>(DateRangeToken token, QueryBuilderResult builder) where T : class
        => _postgres.Visit<T>(token, builder);

    public QueryBuilderResult Visit<T>(IsToken token, QueryBuilderResult builder) where T : class
        => _postgres.Visit<T>(token, builder);

    public QueryBuilderResult Visit<T>(MetaIncludeToken token, QueryBuilderResult builder) where T : class
        => _postgres.Visit<T>(token, builder);

    public QueryBuilderResult Visit<T>(NestedFilterToken token, QueryBuilderResult builder) where T : class
        => _postgres.Visit<T>(token, builder);

    public QueryBuilderResult Visit<T>(ArrayOperationToken token, QueryBuilderResult builder) where T : class
        => _postgres.Visit<T>(token, builder);
}
