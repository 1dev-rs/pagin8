using System.Globalization;
using _1Dev.Pagin8.Internal.Exceptions.Base;
using _1Dev.Pagin8.Internal.Exceptions.StatusCodes;
using _1Dev.Pagin8.Internal.Tokenizer.Operators;
using _1Dev.Pagin8.Internal.Tokenizer.Tokens;
using Pagin8.Internal.Configuration;

namespace _1Dev.Pagin8.Internal.Helpers;

/// <summary>
/// The filter value of a [CompareTime] property: the interval of its own precision, [Start, End).
/// Every provider compares with the bounds below, so the rows are the same on each; a value
/// without a time is a whole day, which is what DATE(col) op DATE(value) selects.
/// </summary>
internal readonly record struct TimeInterval(DateTime Start, DateTime End)
{
    private static readonly (string Format, Func<DateTime, DateTime> End)[] Formats =
    [
        ("yyyy-MM-dd", d => d.AddDays(1)),
        ("yyyy-MM-ddTHH:mm", d => d.AddMinutes(1)),
        ("yyyy-MM-ddTHH:mm:ss", d => d.AddSeconds(1)),
        ("yyyy-MM-ddTHH:mm:ss.fff", d => d.AddMilliseconds(1)),
    ];

    public static TimeInterval Parse(string value)
    {
        foreach (var (format, end) in Formats)
        {
            if (DateTime.TryParseExact(value, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var start))
                return new TimeInterval(start, end(start));
        }

        throw new Pagin8Exception(Pagin8StatusCode.Pagin8_InvalidComparison.Code,
            $"Invalid date format '{value}'. Expected yyyy-MM-dd, yyyy-MM-ddTHH:mm, yyyy-MM-ddTHH:mm:ss or yyyy-MM-ddTHH:mm:ss.fff.");
    }

    /// <summary>
    /// The conditions on the column, AND-ed: eq inside the interval, gt after it, gte from its
    /// start, lt before it, lte up to its end.
    /// </summary>
    public IReadOnlyList<(string Operator, DateTime Bound)> Bounds(ComparisonOperator op) => op switch
    {
        ComparisonOperator.Equals => [(">=", Start), ("<", End)],
        ComparisonOperator.GreaterThan => [(">=", End)],
        ComparisonOperator.GreaterThanOrEqual => [(">=", Start)],
        ComparisonOperator.LessThan => [("<", Start)],
        ComparisonOperator.LessThanOrEqual => [("<", End)],
        _ => throw new Pagin8Exception(Pagin8StatusCode.Pagin8_UnsupportedComparison.Code)
    };

    /// <summary>
    /// The SQL every dialect uses for a [CompareTime] comparison: plain comparisons of the column,
    /// so an index on it applies; not. keeps NULL rows, as every negated comparison does.
    /// </summary>
    /// <param name="column">The column, formatted for the dialect.</param>
    /// <param name="token">The comparison.</param>
    /// <param name="bound">A bound as the dialect takes it; the parameter itself when null.</param>
    public static FormattableString BuildSql(string column, ComparisonToken token, Func<DateTime, FormattableString>? bound = null)
    {
        var bounds = Parse(token.Value).Bounds(token.Operator);
        bound ??= b => $"{b}";

        var query = bounds.Count == 1
            ? (FormattableString)$"{column:raw} {bounds[0].Operator:raw} {bound(bounds[0].Bound)}"
            : (FormattableString)$"({column:raw} {bounds[0].Operator:raw} {bound(bounds[0].Bound)} AND {column:raw} {bounds[1].Operator:raw} {bound(bounds[1].Bound)})";

        if (!token.IsNegated)
            return query;

        return $"(({EngineDefaults.Config.Negation:raw} ({query})) OR {column:raw} IS NULL)";
    }
}
