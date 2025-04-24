using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using _1Dev.Pagin8.Internal.Exceptions.Base;
using _1Dev.Pagin8.Internal.Exceptions.StatusCodes;
using _1Dev.Pagin8.Internal.Tokenizer.Tokens;
using Pagin8.Internal.Configuration;

namespace _1Dev.Pagin8.Internal.Helpers;
public static class TokenHelper
{
    private static readonly Dictionary<int, char> DelimitersByLevel = new()
    {
        { 1, '&' },
        { 2, ',' }
    };

    public static string DateRangePattern => $@"^(?<field>\w+)=(?<negation>{EngineDefaults.Config.Negation}\.)?(?<operator>{string.Join("|", EngineDefaults.Config.DateRangeOperators)})\.(?<value>\d+)(?<range>[dmwy])(?<exact>e)?(?<strict>s)?(?:\^(?<comment>.+))?$";

    public static string NestedDateRangePattern => $@"^(?<field>\w+)\.(?<negation>{EngineDefaults.Config.Negation}\.)?(?<operator>{string.Join("|", EngineDefaults.Config.DateRangeOperators)})\.(?<value>\d+)(?<range>[dmwy])(?<exact>e)?(?<strict>s)?(?:\^(?<comment>.+))?$";

    public static string ComparisonPattern => $@"^(?<field>[^.]+)=(?<negation>{EngineDefaults.Config.Negation}\.)?(?<operator>({string.Join("|", EngineDefaults.Config.ComparisonOperators)}))\.(?<val>.*?)(?:\^(?<comment>.+))?$";

    public static string NestedComparisonPattern => $@"^(?<field>[^.]+)\.(?<negation>{EngineDefaults.Config.Negation}\.)?(?<operator>({string.Join("|", EngineDefaults.Config.ComparisonOperators)}))\.(?<val>.*?)(?:\^(?<comment>.+))?$";

    public static string IsPattern => $@"^(?<field>[^.]+)=(?<operator>{EngineDefaults.Config.IsOperator})\.(?<negation>{EngineDefaults.Config.Negation}\.)?(?<val>.*?)(?:\^(?<comment>.+))?$";

    public static string NestedIsPattern => $@"^(?<field>[^.]+)\.(?<operator>{EngineDefaults.Config.IsOperator})\.(?<negation>{EngineDefaults.Config.Negation}\.)?(?<val>.*?)(?:\^(?<comment>.+))?$";

    public static string InPattern => $@"^(?<field>[^.=]+)=(?:(?<negation>{EngineDefaults.Config.Negation})\.)?(?:(?<comparison>eq|is|gt|gte|lt|lte|stw|enw|like|cs)\.)?(?<operator>{EngineDefaults.Config.InOperator})\.(?<values>\(([^)]+)\)|[^)]+)(?:\^(?<comment>.+))?$";

    public static string NestedInPattern => $@"^(?<field>[^.=]+)(?:\.(?<negation>{EngineDefaults.Config.Negation}))?(?:\.(?<comparison>eq|is|gt|gte|lt|lte|stw|enw|like|cs))?\.(?<operator>{EngineDefaults.Config.InOperator})\.(?<values>\(([^)]+)\)|[^)]+)(?:\^(?<comment>.+))?$";

    public static string GroupingPattern => $@"^(?<negation>{EngineDefaults.Config.Negation}\.)?(?<operator>{string.Join("|", EngineDefaults.Config.GroupOperators)})=\((?<val>.*)\)(?:\^(?<comment>.+))?$";

    public static string NestedGroupingPattern => $@"^(?<negation>{EngineDefaults.Config.Negation}\.)?(?<operator>{string.Join("|", EngineDefaults.Config.GroupOperators)})\((?<val>.*)\)(?:\^(?<comment>.+))?$";

    public static string SortExpressionPlainPattern => $@"(?<field>\$?\w+)\.(?<order>{string.Join("|", EngineDefaults.Config.PossibleOrder)})";

    public static string SortExpressionPagingPattern => $@"(?<field>\$?\w+)\.(?<order>{string.Join("|", EngineDefaults.Config.PossibleOrder)})\.(?<lastValue>.*?)(?=(\.\w+\.)|(?:,\w+\.)|$)";

    public static string PagingSectionPattern => @"paging=\((.*)\)";

    public static string NestedFilterPattern => @"^(?<field>\w+)\.with=\((?<conditions>[\s\S]*)\)(?:\^(?<comment>.+))?$";

    public static string ArraySectionPattern => @"^(?<field>\w+)\.(?<mode>incl|excl)\((?<values>.*)\)(?:\^(?<comment>.+))?$";

    public static string SortExpressionKeyPattern => @$"^\{QueryConstants.KeyPlaceholder}\.([^\s]+)";

    public static string MetaIncludePattern
    {
        get
        {
            var properties = EngineDefaults.Config.MetaInclude;
            if (properties.Count == 0)
                return "";

            var pattern = "^metaInclude=";

            pattern += "(" + string.Join("|", properties.Select(PropertyPattern)) + ")*$";

            return pattern;

            static string PropertyPattern(string prop) => $"(?:(?<{prop}>{prop})(?:,|$))";
        }
    }

    public static char TakeDelimiter(int nestingLevel) =>
        nestingLevel <= DelimitersByLevel.Count ?
            DelimitersByLevel[nestingLevel] :
            DelimitersByLevel[DelimitersByLevel.Count];

    public static IEnumerable<string> SplitAtDelimiters(string input, char delimiter)
    {
        var parts = new List<string>();

        var stack = new Stack<char>();
        var currentPart = new StringBuilder();

        foreach (var c in input)
        {
            switch (c)
            {
                case '(':
                    stack.Push(c);
                    currentPart.Append(c);
                    break;
                case ')':
                    stack.Pop();
                    currentPart.Append(c);
                    break;
                default:
                    {
                        if (stack.Count == 0 && delimiter == c)
                        {
                            parts.Add(currentPart.ToString().Trim());
                            currentPart.Clear();
                        }
                        else
                        {
                            currentPart.Append(c);
                        }

                        break;
                    }
            }
        }

        parts.Add(currentPart.ToString().Trim());

        return parts.ToArray();
    }

    public static void RemoveTrailingComma(StringBuilder sb)
    {
        if (sb[^1] == ',')
            sb.Length--;
    }

    public static string Normalize(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new Pagin8Exception(Pagin8StatusCode.Pagin8_MissingQuery.Code);
        }

        if (query.StartsWith("?"))
        {
            query = query.TrimStart('?');
        }

        return HttpUtility.UrlDecode(query);
    }


    public static bool TryAddDefault<T>(this ICollection<Token> tokens, T defaultToken) where T : Token
    {
        if (tokens.All(x => x.GetType() != typeof(T)))
        {
            tokens.Add(defaultToken);
            return true;
        }

        return false;
    }

    public static bool IsComparisonOperation(string query) => Regex.IsMatch(query, ComparisonPattern) || Regex.IsMatch(query, NestedComparisonPattern);

    public static bool IsIsOperation(string query) => Regex.IsMatch(query, IsPattern) || Regex.IsMatch(query, NestedIsPattern);

    public static bool IsInOperation(string query) => Regex.IsMatch(query, InPattern) || Regex.IsMatch(query, NestedInPattern);

    public static bool IsGroupingOperation(string query) => Regex.IsMatch(query, GroupingPattern) || Regex.IsMatch(query, NestedGroupingPattern);

    public static bool IsSortOperation(string query) => query.StartsWith("sort(", StringComparison.OrdinalIgnoreCase);

    public static bool IsLimitOperation(string query) => query.StartsWith("limit.", StringComparison.OrdinalIgnoreCase);

    public static bool IsSelectOperation(string query) => query.StartsWith("select=", StringComparison.OrdinalIgnoreCase);

    public static bool IsPagingOperation(string query) => query.StartsWith("paging=", StringComparison.OrdinalIgnoreCase);

    public static bool IsCountOperation(string query) => query.StartsWith("count.", StringComparison.OrdinalIgnoreCase);

    public static bool IsDateRangeOperation(string query) => Regex.IsMatch(query, DateRangePattern) || Regex.IsMatch(query, NestedDateRangePattern);

    public static bool IsMetadataOperation(string query) => query.StartsWith("metaInclude=", StringComparison.OrdinalIgnoreCase);

    public static bool IsNestedFilterOperation(string query) => Regex.IsMatch(query, NestedFilterPattern);

    public static bool IsArrayOperation(string query) => Regex.IsMatch(query, ArraySectionPattern);
}
