using _1Dev.Pagin8.Internal.DateProcessor;
using _1Dev.Pagin8.Internal.Metadata.Models;
using _1Dev.Pagin8.Internal.Tokenizer.Contracts;
using _1Dev.Pagin8.Internal.Tokenizer.Operators;
using _1Dev.Pagin8.Internal.Tokenizer.Tokens;
using _1Dev.Pagin8.Internal.Tokenizer.Tokens.Sort;
using _1Dev.Pagin8.Internal.Validators;
using AspNet.Transliterator;
using Internal.Configuration;
using Pagin8.Internal.Configuration;

namespace _1Dev.Pagin8.Internal.Visitors;

/// <summary>
/// DuckDB SQL emission. DuckDB accepts the PostgreSQL dialect pagin8 emits for flat models
/// (ILIKE, ESCAPE, DATE(), = ANY(ARRAY[...]), ::casts, LIMIT, the keyset OR chain), so every
/// token is delegated to <see cref="NpgsqlTokenVisitor"/>. The parameter marker differs
/// (@p0 against DuckDB's $p0) and is rewritten when the command runs, by the Backend's
/// DuckDB connection. A DuckDB-specific difference belongs in the matching method below.
/// Nested tokens (and=/or= members, paging parts) come back to this visitor, so its rules apply
/// at every depth.
/// </summary>
public class DuckDbTokenVisitor : ISqlTokenVisitor
{
    private readonly IPagin8MetadataProvider _metadata;
    private readonly NpgsqlTokenVisitor _postgres;

    public DuckDbTokenVisitor(IPagin8MetadataProvider metadata, IDateProcessor dateProcessor)
    {
        _metadata = metadata;
        _postgres = new NpgsqlTokenVisitor(metadata, dateProcessor) { ChildVisitor = this };
    }

    // Text comparisons as DuckDB's string functions rather than ILIKE, which DuckDB evaluates about
    // three times slower than contains(lower(col), v) on a column scan, and which keeps DuckDB from
    // using a column's statistics or bloom filter for an equality. The rows matched are the same:
    // - eq/cs/stw/enw with a value that is only ASCII characters other than letters (account
    //   numbers, references, codes) compare the column as it is: lower() changes letters only, so
    //   lower(col) = v, col ILIKE v and col = v select the same rows for such a value;
    // - cs/stw/enw with letters compare lower(col) with the lower-cased value, which is what ILIKE
    //   does (eq with letters is left to the PostgreSQL emission);
    // - the value is taken literally (no LIKE escaping: these functions have no wildcards).
    // not. and NULL rows follow the PostgreSQL emission (NOT ..., then OR col IS NULL).
    public QueryBuilderResult Visit<T>(ComparisonToken token, QueryBuilderResult builder) where T : class
    {
        if (!TryBuildTextMatch<T>(token, out var query))
            return _postgres.Visit<T>(token, builder);

        builder.Builder += query;
        return builder;
    }

    public QueryBuilderResult Visit<T>(GroupToken token, QueryBuilderResult builder) where T : class
        => _postgres.Visit<T>(token, builder);

    // PostgreSQL's `col ILIKE ANY(ARRAY[...])` for text is refused by DuckDB ("Unsupported comparison
    // ~~* for ANY/ALL"), so a text IN becomes the same values as eq comparisons: OR-ed, or for
    // not.in AND-ed negations, each of which keeps NULL rows exactly like the PostgreSQL form does.
    public QueryBuilderResult Visit<T>(InToken token, QueryBuilderResult builder) where T : class
    {
        if (!string.IsNullOrEmpty(token.JsonPath) || _metadata.GetTypeCodeForProperty<T>(token.Field) != TypeCode.String)
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

    private bool TryBuildTextMatch<T>(ComparisonToken token, out FormattableString query) where T : class
    {
        query = $"";

        if (!string.IsNullOrEmpty(token.JsonPath)
            || token.Operator is not (ComparisonOperator.Equals or ComparisonOperator.Contains or ComparisonOperator.StartsWith or ComparisonOperator.EndsWith)
            || _metadata.GetTypeCodeForProperty<T>(token.Field) != TypeCode.String)
            return false;

        ComparisonValidator.EnsureTypeCodeOperatorValid(TypeCode.String, token.Operator);

        // The value as the PostgreSQL emission compares it, before its LIKE pattern is built.
        var value = Transliteration.ToLowerBoldLatin(token.Value);
        var caseless = IsCaseless(value);

        if (token.Operator is ComparisonOperator.Equals)
        {
            // Without UseIndexFriendlyTextComparisons, eq is `col ILIKE v` with v unescaped, where
            // % and _ match anything; such a value keeps that emission.
            if (!caseless || (!Pagin8Runtime.Config.UseIndexFriendlyTextComparisons && value.IndexOfAny(['%', '_']) >= 0))
                return false;
        }

        var column = NpgsqlTokenVisitor.TryFormatColumnName(_metadata.GetColumnInfo<T>(token.Field).Name);
        var subject = caseless ? column : $"lower({column})";
        var negation = token.IsNegated ? $"{EngineDefaults.Config.Negation} " : "";

        query = token.Operator switch
        {
            ComparisonOperator.Equals => $"{negation:raw}({subject:raw} = {value})",
            ComparisonOperator.Contains => $"{negation:raw}contains({subject:raw}, {value})",
            ComparisonOperator.StartsWith => $"{negation:raw}starts_with({subject:raw}, {value})",
            _ => $"{negation:raw}ends_with({subject:raw}, {value})"
        };

        NpgsqlTokenVisitor.TryHandleNullColumns(column, token, ref query);
        query = $"({query})";
        return true;
    }

    // Only ASCII characters that are not letters: lower() leaves every such character as it is and
    // turns no other character into one of them, so comparing the column with or without lower()
    // selects the same rows.
    private static bool IsCaseless(string value)
    {
        foreach (var c in value)
        {
            if (c > 0x7F || char.IsAsciiLetter(c))
                return false;
        }

        return true;
    }
}
