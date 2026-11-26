using System.Globalization;
using _1Dev.Pagin8.Internal.DateProcessor;
using _1Dev.Pagin8.Internal.Exceptions.Base;
using _1Dev.Pagin8.Internal.Exceptions.StatusCodes;
using _1Dev.Pagin8.Internal.Helpers;
using _1Dev.Pagin8.Internal.Metadata.Models;
using _1Dev.Pagin8.Internal.Tokenizer.Contracts;
using _1Dev.Pagin8.Internal.Tokenizer.Operators;
using _1Dev.Pagin8.Internal.Tokenizer.Tokens;
using _1Dev.Pagin8.Internal.Tokenizer.Tokens.Sort;
using _1Dev.Pagin8.Internal.Validators;
using AspNet.Transliterator;
using Internal.Configuration;
using Pagin8.Internal.Configuration;
using QueryBuilder = InterpolatedSql.Dapper.SqlBuilders.QueryBuilder;

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
    private readonly bool _sortByTranslit;

    public DuckDbTokenVisitor(IPagin8MetadataProvider metadata, IDateProcessor dateProcessor)
        : this(metadata, dateProcessor, sortByTranslit: false)
    {
    }

    /// <param name="sortByTranslit">Sort and page a [UseTranslit] text property by its translit column.</param>
    public DuckDbTokenVisitor(IPagin8MetadataProvider metadata, IDateProcessor dateProcessor, bool sortByTranslit)
    {
        _metadata = metadata;
        _sortByTranslit = sortByTranslit;
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

    // With sortByTranslit, a sort by a [UseTranslit] text property orders and pages by its translit
    // column, the one its filters use: DuckDB sorts text binary, so by the plain column "Zemun" comes
    // before "beograd" and "Čačak" after both. The cursor value is transliterated as a filter value
    // is. Every other sort is the PostgreSQL emission; this one repeats it with the translit column.
    public QueryBuilderResult Visit<T>(SortToken token, QueryBuilderResult builder) where T : class
    {
        if (!_sortByTranslit)
            return _postgres.Visit<T>(token, builder);

        MapPlaceholderToKey<T>(token.SortExpressions);
        if (token.SortExpressions.All(e => GetSortTranslitColumn<T>(e.Field) is null))
            return _postgres.Visit<T>(token, builder);

        if (token.SortExpressions.Any(e => !_metadata.IsFieldSortable<T>(e.Field)))
            throw new Pagin8Exception(Pagin8StatusCode.Pagin8_ColumnNotSortable.Code);

        BuildSortConditions<T>(builder.Builder, token.SortExpressions);
        BuildSortOrder<T>(builder.Builder, token.SortExpressions);
        return builder;
    }

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

    private void MapPlaceholderToKey<T>(IReadOnlyCollection<SortExpression> sortExpressions) where T : class
    {
        if (sortExpressions == null) throw new Pagin8Exception(Pagin8StatusCode.Pagin8_MissingSortExpressions.Code);

        foreach (var keyExpression in sortExpressions.Where(x => x.Field == QueryConstants.KeyPlaceholder))
            keyExpression.Field = _metadata.GetEntityKey<T>();
    }

    // The translit column a sort by this field uses, or null when it uses the field's own column.
    private string? GetSortTranslitColumn<T>(string field) where T : class
    {
        if (_metadata.GetTypeCodeForProperty<T>(field) != TypeCode.String) return null;

        var columnInfo = _metadata.GetColumnInfo<T>(field);
        return columnInfo.IsTranslit ? columnInfo.Name : null;
    }

    // NpgsqlTokenVisitor.BuildSortConditions, with the translit column and value for a translit field.
    private void BuildSortConditions<T>(QueryBuilder builder, IReadOnlyList<SortExpression> sortExpressions) where T : class
    {
        if (sortExpressions.All(x => string.IsNullOrEmpty(x.LastValue))) return;

        builder += $"(";
        for (var i = 0; i < sortExpressions.Count; i++)
        {
            if (i > 0) builder += $" OR ";

            builder += $"(";

            for (var j = 0; j <= i; j++)
            {
                var expression = sortExpressions[j];
                var field = expression.Field;

                if (j > 0) builder += $" AND";

                var translitColumn = GetSortTranslitColumn<T>(field);
                var formattedValue = FormatColumnValue<T>(field, expression.LastValue, translitColumn is not null);
                var formattedName = NpgsqlTokenVisitor.TryFormatColumnName(translitColumn ?? field);
                var @operator = j == i ? (expression.SortOrder == SortOrder.Ascending ? ">" : "<") : "=";
                var columnInfo = _metadata.GetColumnInfo<T>(field, useTranslit: false);
                var typeCode = _metadata.GetTypeCodeForProperty<T>(field);

                if (expression.LastValue is null && columnInfo.IsNullAllowed)
                {
                    var coalesce = GetCoalesceMinValue(typeCode);
                    if (@operator is "=")
                        builder += $" ({formattedName:raw} IS NULL OR {formattedName:raw} = {coalesce})";
                    else
                        builder += $" ({formattedName:raw} IS NOT NULL AND {formattedName:raw} {@operator:raw} {coalesce})";
                }
                else if (formattedValue is null && @operator is "=")
                {
                    builder += $" {formattedName:raw} IS NULL";
                }
                else if (columnInfo.IsNullAllowed)
                {
                    if (formattedValue is null)
                    {
                        builder += $" {formattedName:raw} IS NULL";
                    }
                    else
                    {
                        var coalesce = GetCoalesceMinValue(typeCode);
                        builder += $" (({formattedName:raw} IS NULL AND {coalesce} {@operator:raw} {formattedValue}) OR ({formattedName:raw} IS NOT NULL AND {formattedName:raw} {@operator:raw} {formattedValue}))";
                    }
                }
                else
                {
                    builder += $" {formattedName:raw} {@operator:raw} {formattedValue}";
                }
            }

            builder += $")";
        }
        builder += $")";
    }

    // NpgsqlTokenVisitor.BuildSortOrder, with the translit column for a translit field.
    private void BuildSortOrder<T>(QueryBuilder builder, IReadOnlyList<SortExpression> sortExpressions) where T : class
    {
        builder += $"ORDER BY ";

        for (var index = 0; index < sortExpressions.Count; index++)
        {
            var expression = sortExpressions[index];
            if (index > 0) builder += $",";

            var columnInfo = _metadata.GetColumnInfo<T>(expression.Field, useTranslit: false);
            var typeCode = _metadata.GetTypeCodeForProperty<T>(columnInfo.Name);
            var formattedName = NpgsqlTokenVisitor.TryFormatColumnName(GetSortTranslitColumn<T>(expression.Field) ?? columnInfo.Name);
            var direction = expression.SortOrder.GetQueryFromSortOrder().ToUpper();

            if (columnInfo.IsNullAllowed)
            {
                var coalesce = GetCoalesceMinValue(typeCode);
                var nullPosition = expression.SortOrder == SortOrder.Ascending ? "NULLS FIRST" : "NULLS LAST";
                builder += $"COALESCE({formattedName:raw}, {coalesce}) {direction:raw} {nullPosition:raw}";
            }
            else
            {
                builder += $"{formattedName:raw} {direction:raw}";
            }
        }
    }

    // NpgsqlTokenVisitor.FormatColumnValue; a translit field's value is transliterated as a filter value is.
    private dynamic? FormatColumnValue<T>(string field, string? value, bool isTranslit) where T : class
    {
        var typeCode = _metadata.GetTypeCodeForProperty<T>(field);

        if (string.IsNullOrEmpty(value))
        {
            if (typeCode == TypeCode.String)
                return value == null ? null : string.Empty;
            return null;
        }

        return typeCode switch
        {
            TypeCode.String when isTranslit => Transliteration.ToLowerBoldLatin(value),
            TypeCode.String => _metadata.GetColumnInfo<T>(field, useTranslit: false).SkipCursorTransliteration ? value : Transliteration.CyrlToLatin(value),
            TypeCode.DateTime => DateTime.TryParse(value, out var date) ? (DateTime?)date : throw new Pagin8Exception(Pagin8StatusCode.Pagin8_InvalidComparison.Code, $"Cannot format value {value} as DateTime"),
            TypeCode.Int16 => short.TryParse(value, out var shortValue) ? (short?)shortValue : throw new Pagin8Exception(Pagin8StatusCode.Pagin8_InvalidComparison.Code, $"Cannot format value {value} as Int16"),
            TypeCode.Int32 => int.TryParse(value, out var intValue) ? (int?)intValue : throw new Pagin8Exception(Pagin8StatusCode.Pagin8_InvalidComparison.Code, $"Cannot format value {value} as Int32"),
            TypeCode.Int64 => long.TryParse(value, out var longValue) ? (long?)longValue : throw new Pagin8Exception(Pagin8StatusCode.Pagin8_InvalidComparison.Code, $"Cannot format value {value} as Int64"),
            TypeCode.Double => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var doubleValue) ? (double?)doubleValue : throw new Pagin8Exception(Pagin8StatusCode.Pagin8_InvalidComparison.Code, $"Cannot format value {value} as Double"),
            TypeCode.Boolean => bool.TryParse(value, out var boolValue) ? (bool?)boolValue : throw new Pagin8Exception(Pagin8StatusCode.Pagin8_InvalidComparison.Code, $"Cannot format value {value} as Boolean"),
            TypeCode.Char => char.TryParse(value, out var charValue) ? Transliteration.ToBoldLatin(charValue.ToString()) : throw new Pagin8Exception(Pagin8StatusCode.Pagin8_InvalidComparison.Code, $"Cannot format value {value} as Char"),
            TypeCode.Decimal => decimal.TryParse(value, CultureInfo.InvariantCulture, out var decimalValue) ? (decimal?)decimalValue : throw new Pagin8Exception(Pagin8StatusCode.Pagin8_InvalidComparison.Code, $"Cannot format value {value} as Decimal"),
            _ => throw new Pagin8Exception(Pagin8StatusCode.Pagin8_UnsupportedComparison.Code, $"Cannot format values for TypeCode {typeCode}")
        };
    }

    private static object GetCoalesceMinValue(TypeCode typeCode) => typeCode switch
    {
        TypeCode.String or TypeCode.Char => "''",
        TypeCode.Int16 or TypeCode.UInt16 or TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Int64 or TypeCode.UInt64
            or TypeCode.Double or TypeCode.Single or TypeCode.Decimal => -1,
        TypeCode.DateTime => DateTime.MinValue,
        TypeCode.Boolean => false,
        _ => throw new Pagin8Exception(Pagin8StatusCode.Pagin8_ColumnNotSortable.Code, $"Sorting nullable columns is not supported for type code: {typeCode}")
    };

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
