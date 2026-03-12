using _1Dev.Pagin8.Internal.Configuration;
using _1Dev.Pagin8.Internal.Tokenizer.Tokens.Sort;
using Internal.Configuration;

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

    private static bool IsSqlServer => Pagin8Runtime.Config.DatabaseType == DatabaseType.SqlServer;

    private static readonly Dictionary<ComparisonOperator, string> _comparisonSqlMapPostgre = new()
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

    private static readonly Dictionary<ComparisonOperator, string> _comparisonSqlMapSqlServer = new()
    {
        { ComparisonOperator.Equals, "=" },
        { ComparisonOperator.GreaterThan, ">" },
        { ComparisonOperator.GreaterThanOrEqual, ">=" },
        { ComparisonOperator.LessThan, "<" },
        { ComparisonOperator.LessThanOrEqual, "<=" },
        { ComparisonOperator.Like, "LIKE" },
        { ComparisonOperator.Is, "IS" },
        { ComparisonOperator.StartsWith, "LIKE" },
        { ComparisonOperator.EndsWith, "LIKE" },
        { ComparisonOperator.Contains, "LIKE" },
        { ComparisonOperator.In, "IN" },
        { ComparisonOperator.Between, "BETWEEN" }
    };

    public static Dictionary<ComparisonOperator, string> ComparisonSqlMap =>
        IsSqlServer ? _comparisonSqlMapSqlServer : _comparisonSqlMapPostgre;

    private static readonly Dictionary<ComparisonOperator, string> _caseSensitiveComparisonSqlMapPostgre = new()
    {
        { ComparisonOperator.Equals, "ILIKE" },
        // Note: For IN operator with text values, SQL Server uses standard IN syntax
        // The actual value formatting is handled in the visitor (SqlServerTokenVisitor or NpgsqlTokenVisitor)
        { ComparisonOperator.In, "IN" }
    };

    private static readonly Dictionary<ComparisonOperator, string> _caseSensitiveComparisonSqlMapSqlServer = new()
    {
        { ComparisonOperator.Equals, "LIKE" },
        // Note: For IN operator with text values, SQL Server uses standard IN syntax
        // The actual value formatting is handled in the visitor (SqlServerTokenVisitor or NpgsqlTokenVisitor)
        { ComparisonOperator.In, "IN" }
    };

    public static Dictionary<ComparisonOperator, string> CaseSensitiveComparisonSqlMap =>
        IsSqlServer ? _caseSensitiveComparisonSqlMapSqlServer : _caseSensitiveComparisonSqlMapPostgre;

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

    private static readonly Dictionary<ComparisonOperator, string> _negatedOperatorSqlMapPostgre = new()
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

    private static readonly Dictionary<ComparisonOperator, string> _negatedOperatorSqlMapSqlServer = new()
    {
        { ComparisonOperator.Equals, "!=" },
        { ComparisonOperator.Like, "NOT LIKE" },
        { ComparisonOperator.StartsWith, "NOT LIKE" },
        { ComparisonOperator.EndsWith, "NOT LIKE" },
        { ComparisonOperator.Contains, "NOT LIKE" },
        { ComparisonOperator.Is, "IS NOT" },
        { ComparisonOperator.In, "NOT IN" },
        { ComparisonOperator.Between, "NOT BETWEEN" },
    };

    public static Dictionary<ComparisonOperator, string> NegatedOperatorSqlMap =>
        IsSqlServer ? _negatedOperatorSqlMapSqlServer : _negatedOperatorSqlMapPostgre;

    private static readonly Dictionary<ComparisonOperator, string> _negatedCaseSensitiveSqlMapPostgre = new()
    {
        { ComparisonOperator.Equals, "NOT ILIKE" },
        // Note: For NOT IN operator with text values, SQL Server uses standard NOT IN syntax
        // The actual value formatting is handled in the visitor (SqlServerTokenVisitor or NpgsqlTokenVisitor)
        { ComparisonOperator.In, "NOT IN" }
    };

    private static readonly Dictionary<ComparisonOperator, string> _negatedCaseSensitiveSqlMapSqlServer = new()
    {
        { ComparisonOperator.Equals, "NOT LIKE" },
        // Note: For NOT IN operator with text values, SQL Server uses standard NOT IN syntax
        // The actual value formatting is handled in the visitor (SqlServerTokenVisitor or NpgsqlTokenVisitor)
        { ComparisonOperator.In, "NOT IN" }
    };

    public static Dictionary<ComparisonOperator, string> NegatedCaseSensitiveOperatorSqlMap =>
        IsSqlServer ? _negatedCaseSensitiveSqlMapSqlServer : _negatedCaseSensitiveSqlMapPostgre;

    public static readonly Dictionary<string, ArrayOperator> ArrayOperatorMap = new()
    {
        { "incl", ArrayOperator.Include },
        { "excl", ArrayOperator.Exclude }
    };

    public static readonly Dictionary<ArrayOperator, string> ReverseArrayOperatorMap = ArrayOperatorMap.ToDictionary(kv => kv.Value, kv => kv.Key);
}
