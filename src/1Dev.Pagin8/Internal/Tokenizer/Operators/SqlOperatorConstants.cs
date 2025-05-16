using _1Dev.Pagin8.Internal.Tokenizer.Tokens.Sort;

// ReSharper disable InconsistentNaming

namespace _1Dev.Pagin8.Internal.Tokenizer.Operators;
public static class SqlOperatorConstants
{
    public static readonly Dictionary<string, ComparisonOperator> QueryComparisonMap = new()
    {
        { "eq", ComparisonOperator.Equals },
        { "is", ComparisonOperator.Is },
        { "gt", ComparisonOperator.GreaterThan },
        { "gte", ComparisonOperator.GreaterThanOrEqual },
        { "lt", ComparisonOperator.LessThan },
        { "lte", ComparisonOperator.LessThanOrEqual },
        { "like", ComparisonOperator.Like},
        { "stw", ComparisonOperator.StartsWith },
        { "enw", ComparisonOperator.EndsWith },
        { "cs", ComparisonOperator.Contains }
    };

    public static readonly Dictionary<ComparisonOperator, string> ReverseQueryComparisonMap = QueryComparisonMap.ToDictionary(kv => kv.Value, kv => kv.Key);

    public static readonly Dictionary<ComparisonOperator, string> ComparisonSqlMap = new()
    {
        { ComparisonOperator.Equals, "=" },
        { ComparisonOperator.GreaterThan, ">" },
        { ComparisonOperator.GreaterThanOrEqual, ">=" },
        { ComparisonOperator.LessThan, "<" },
        { ComparisonOperator.LessThanOrEqual, "<=" },
        { ComparisonOperator.Like, "ILIKE" },
        { ComparisonOperator.Is, "IS" },
        { ComparisonOperator.StartsWith, "ILIKE" },
        { ComparisonOperator.EndsWith, "ILIKE" },
        { ComparisonOperator.Contains, "ILIKE" },
        { ComparisonOperator.In, "IN" },
        { ComparisonOperator.Between, "BETWEEN" }
    };

    public static readonly Dictionary<ComparisonOperator, string> CaseSensitiveComparisonSqlMap = new()
    {
        { ComparisonOperator.Equals, "ILIKE" },
        { ComparisonOperator.In, "ILIKE ANY (ARRAY[{0}])" }
    };

    public static readonly Dictionary<string, NestingOperator> QueryNestingMap = new()
    {
        { "and", NestingOperator.And },
        { "or", NestingOperator.Or}
    };

    public static readonly Dictionary<NestingOperator, string> ReverseQueryNestingMap = QueryNestingMap.ToDictionary(kv => kv.Value, kv => kv.Key);

    public static readonly Dictionary<string, SortOrder> QuerySortOrderMap = new()
    {
        { "asc", SortOrder.Ascending },
        { "desc", SortOrder.Descending}
    };

    public static readonly Dictionary<SortOrder, string> ReverseQuerySortOrderMap = QuerySortOrderMap.ToDictionary(kv => kv.Value, kv => kv.Key);


    public static readonly Dictionary<NestingOperator, string> NestingSqlMap = new()
    {
        { NestingOperator.And, "AND "},
        { NestingOperator.Or, "OR "}
    };

    public static readonly Dictionary<string, DateRangeOperator> DateRangeOperatorMap = new()
    {
        { "for", DateRangeOperator.For },
        { "ago", DateRangeOperator.Ago }
    };

    public static readonly Dictionary<DateRangeOperator, string> ReverseDateRangeOperatorMap = DateRangeOperatorMap.ToDictionary(kv => kv.Value, kv => kv.Key);

    public static readonly Dictionary<char, DateRange> DateRangeMap = new()
    {
        { 'd', DateRange.Day },
        { 'w', DateRange.Week },
        { 'm', DateRange.Month },
        { 'y', DateRange.Year },
    };

    public static readonly Dictionary<DateRange, char> ReverseDateRangeMap = DateRangeMap.ToDictionary(kv => kv.Value, kv => kv.Key);

    public static readonly Dictionary<char, int> DateRangeMapSeverity = new()
    {
        { 'd', 1 },
        { 'w', 7 },
        { 'm', 30 },
        { 'y', 365 },
    };

    public static readonly Dictionary<ComparisonOperator, string> NegatedOperatorSqlMap = new()
    {
        { ComparisonOperator.Equals, "!=" },
        { ComparisonOperator.Like, "NOT ILIKE" },
        { ComparisonOperator.StartsWith, "NOT ILIKE" },
        { ComparisonOperator.EndsWith, "NOT ILIKE" },
        { ComparisonOperator.Contains, "NOT ILIKE" },
        { ComparisonOperator.Is, "IS NOT" },
        { ComparisonOperator.In, "NOT IN" },
        { ComparisonOperator.Between, "NOT BETWEEN" },
    };

    public static readonly Dictionary<ComparisonOperator, string> NegatedCaseSensitiveOperatorSqlMap = new()
    {
        { ComparisonOperator.Equals, "NOT ILIKE" },
        { ComparisonOperator.In, "NOT ({0} ILIKE ANY (ARRAY[{1}]))" }
    };

    public static readonly Dictionary<string, ArrayOperator> ArrayOperatorMap = new()
    {
        { "incl", ArrayOperator.Include },
        { "excl", ArrayOperator.Exclude }
    };

    public static readonly Dictionary<ArrayOperator, string> ReverseArrayOperatorMap = ArrayOperatorMap.ToDictionary(kv => kv.Value, kv => kv.Key);
}
