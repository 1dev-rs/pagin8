using _1Dev.Pagin8.Internal.Exceptions.Base;
using _1Dev.Pagin8.Internal.Exceptions.StatusCodes;
using _1Dev.Pagin8.Internal.Tokenizer.Tokens;
using _1Dev.Pagin8.Internal.Tokenizer.Tokens.Sort;

namespace _1Dev.Pagin8.Internal.Tokenizer.Operators;
public static class SqlOperatorProcessor
{
    #region PublicMethods

    public static ComparisonOperator GetComparisonOperator(this string op)
    {
        if (!SqlOperatorConstants.QueryComparisonMap.TryGetValue(op, out var result))
            throw new Pagin8Exception(Pagin8StatusCode.Pagin8_UnsupportedComparison.Code);

        return result;
    }

    public static string GetQueryFromComparison(this ComparisonOperator op)
    {
        if (!SqlOperatorConstants.ReverseQueryComparisonMap.TryGetValue(op, out var result))
            throw new Pagin8Exception(Pagin8StatusCode.Pagin8_UnsupportedComparison.Code);

        return result;
    }

    public static NestingOperator GetNestingOperator(this string op)
    {
        if (!SqlOperatorConstants.QueryNestingMap.TryGetValue(op, out var result))
            throw new Pagin8Exception(Pagin8StatusCode.Pagin8_UnsupportedComparison.Code);

        return result;
    }

    public static string GetQueryFromNesting(this NestingOperator op)
    {
        if (!SqlOperatorConstants.ReverseQueryNestingMap.TryGetValue(op, out var result))
            throw new Pagin8Exception(Pagin8StatusCode.Pagin8_UnsupportedComparison.Code);

        return result;
    }

    public static SortOrder GetSortOrder(this string op)
    {
        if (!SqlOperatorConstants.QuerySortOrderMap.TryGetValue(op, out var result))
            throw new Pagin8Exception(Pagin8StatusCode.Pagin8_InvalidSortDirection.Code);

        return result;
    }

    public static string GetQueryFromSortOrder(this SortOrder op)
    {
        if (!SqlOperatorConstants.ReverseQuerySortOrderMap.TryGetValue(op, out var result))
            throw new Pagin8Exception(Pagin8StatusCode.Pagin8_InvalidSortDirection.Code);

        return result;
    }

    public static DateRangeOperator GetDateRangeOperator(this string op)
    {
        if (!SqlOperatorConstants.DateRangeOperatorMap.TryGetValue(op, out var result))
            throw new Pagin8Exception(Pagin8StatusCode.Pagin8_UnsupportedDateRangeOperation.Code);

        return result;
    }

    public static string GetQueryFromDateRange(this DateRangeOperator op)
    {
        if (!SqlOperatorConstants.ReverseDateRangeOperatorMap.TryGetValue(op, out var result))
            throw new Pagin8Exception(Pagin8StatusCode.Pagin8_UnsupportedComparison.Code);

        return result;
    }

    public static DateRange GetDateRange(this char range)
    {
        if (!SqlOperatorConstants.DateRangeMap.TryGetValue(range, out var result))
            throw new Pagin8Exception(Pagin8StatusCode.Pagin8_UnsupportedDateRange.Code);

        return result;
    }

    public static int GetDateRangeSeverity(this char range)
    {
        if (!SqlOperatorConstants.DateRangeMapSeverity.TryGetValue(range, out var result))
            throw new Pagin8Exception(Pagin8StatusCode.Pagin8_UnsupportedDateRange.Code);

        return result;
    }

    public static char GetCharFromDateRange(this DateRange range)
    {
        if (!SqlOperatorConstants.ReverseDateRangeMap.TryGetValue(range, out var result))
            throw new Pagin8Exception(Pagin8StatusCode.Pagin8_UnsupportedDateRange.Code);

        return result;
    }

    public static string GetSqlOperator(this GroupToken token)
    {
        if (!SqlOperatorConstants.NestingSqlMap.TryGetValue(token.NestingOperator, out var result))
            throw new Pagin8Exception(Pagin8StatusCode.Pagin8_UnsupportedComparison.Code);

        return result;
    }

    public static string GetSqlOperator(this ComparisonToken token, bool isText)
    {
        var comparisonOperator = token.Operator;
        var supportsCaseSensitivity = comparisonOperator.SupportsCaseSensitivity();

        if (token.IsNegated) return GetNegatedSql(comparisonOperator, supportsCaseSensitivity, isText);

        if (supportsCaseSensitivity && isText)
        {
            return GetCaseSensitiveOperator(comparisonOperator);
        }

        return GetDefaultSqlOperator(comparisonOperator);
    }

    public static string GetSqlOperator(ComparisonOperator op, bool isText, bool isNegated = false)
    {
        var supportsCaseSensitivity = op.SupportsCaseSensitivity();

        if (isNegated) return GetNegatedSql(op, supportsCaseSensitivity, isText);

        if (supportsCaseSensitivity && isText)
        {
            return GetCaseSensitiveOperator(op);
        }

        return GetDefaultSqlOperator(op);
    }


    public static string GetSqlOperator(this InToken token, bool isText)
    {
        const ComparisonOperator comparisonOperator = ComparisonOperator.In;
        var supportsCaseSensitivity = comparisonOperator.SupportsCaseSensitivity();

        if (token.IsNegated) return GetNegatedSql(comparisonOperator, supportsCaseSensitivity, isText);

        if (supportsCaseSensitivity && isText)
        {
            return GetCaseSensitiveOperator(comparisonOperator);
        }

        return GetDefaultSqlOperator(comparisonOperator);
    }

    public static string GetSqlOperator(this DateRangeToken token)
    {
        if (token.IsNegated) return GetNegatedSql(ComparisonOperator.Between);

        if (!SqlOperatorConstants.ComparisonSqlMap.TryGetValue(ComparisonOperator.Between, out var result))
            throw new Pagin8Exception(Pagin8StatusCode.Pagin8_UnsupportedComparison.Code);

        return result;
    }

    public static ArrayOperator GetArrayOperator(this string op)
    {
        if (!SqlOperatorConstants.ArrayOperatorMap.TryGetValue(op, out var result))
            throw new Pagin8Exception(Pagin8StatusCode.Pagin8_UnsupportedComparison.Code);

        return result;
    }

    public static string GetDslOperator(this ArrayOperator token)
    {
        if (!SqlOperatorConstants.ReverseArrayOperatorMap.TryGetValue(token, out var result))
            throw new Pagin8Exception(Pagin8StatusCode.Pagin8_UnsupportedComparison.Code);

        return result;
    }

    #endregion


    #region Private methods

    private static string GetNegatedSql(ComparisonOperator comparisonOperator, bool supportsCaseSensitivity = false, bool isText = false)
    {
        if (supportsCaseSensitivity && isText)
        {
            if (!SqlOperatorConstants.NegatedCaseSensitiveOperatorSqlMap.TryGetValue(comparisonOperator, out var negatedCaseSensitive))
            {
                throw new Pagin8Exception(Pagin8StatusCode.Pagin8_UnsupportedComparison.Code);
            }

            return negatedCaseSensitive;
        }

        if (!SqlOperatorConstants.NegatedOperatorSqlMap.TryGetValue(comparisonOperator, out var negated))
        {
            throw new Pagin8Exception(Pagin8StatusCode.Pagin8_UnsupportedComparison.Code);
        }

        return negated;
    }

    private static bool SupportsCaseSensitivity(this ComparisonOperator op)
    {
        // other string operators are case sensitive by default, these to can be applied to numeric values also -- N.Z
        return op is ComparisonOperator.In or ComparisonOperator.Equals;
    }

    private static string GetDefaultSqlOperator(ComparisonOperator comparisonOperator)
    {
        if (!SqlOperatorConstants.ComparisonSqlMap.TryGetValue(comparisonOperator, out var defaultResult))
        {
            throw new Pagin8Exception(Pagin8StatusCode.Pagin8_UnsupportedComparison.Code);
        }

        return defaultResult;
    }

    private static string GetCaseSensitiveOperator(ComparisonOperator comparisonOperator)
    {
        if (!SqlOperatorConstants.CaseSensitiveComparisonSqlMap.TryGetValue(comparisonOperator, out var caseSensitive))
        {
            throw new Pagin8Exception(Pagin8StatusCode.Pagin8_UnsupportedComparison.Code);
        }

        return caseSensitive;
    }

    #endregion
}
