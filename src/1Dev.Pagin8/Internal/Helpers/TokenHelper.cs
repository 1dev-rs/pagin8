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

    // Compiled once on first use (after EngineDefaults.Config is initialized).
    // RegexOptions.Compiled generates IL at runtime and is ~3-5× faster than interpreted mode.
    private static readonly Lazy<Regex> CompiledDateRange = new(static () => new Regex(DateRangePattern, RegexOptions.Compiled));
    private static readonly Lazy<Regex> CompiledNestedDateRange = new(static () => new Regex(NestedDateRangePattern, RegexOptions.Compiled));
    private static readonly Lazy<Regex> CompiledComparison = new(static () => new Regex(ComparisonPattern, RegexOptions.Compiled));
    private static readonly Lazy<Regex> CompiledNestedComparison = new(static () => new Regex(NestedComparisonPattern, RegexOptions.Compiled));
    private static readonly Lazy<Regex> CompiledIs = new(static () => new Regex(IsPattern, RegexOptions.Compiled));
    private static readonly Lazy<Regex> CompiledNestedIs = new(static () => new Regex(NestedIsPattern, RegexOptions.Compiled));
    private static readonly Lazy<Regex> CompiledIn = new(static () => new Regex(InPattern, RegexOptions.Compiled));
    private static readonly Lazy<Regex> CompiledNestedIn = new(static () => new Regex(NestedInPattern, RegexOptions.Compiled));
    private static readonly Lazy<Regex> CompiledGrouping = new(static () => new Regex(GroupingPattern, RegexOptions.Compiled));
    private static readonly Lazy<Regex> CompiledNestedGrouping = new(static () => new Regex(NestedGroupingPattern, RegexOptions.Compiled));
    private static readonly Lazy<Regex> CompiledNestedFilter = new(static () => new Regex(NestedFilterPattern, RegexOptions.Compiled));
    private static readonly Lazy<Regex> CompiledNestedNestedFilter = new(static () => new Regex(NestedNestedFilterPattern, RegexOptions.Compiled));
    private static readonly Lazy<Regex> CompiledArray = new(static () => new Regex(ArrayPattern, RegexOptions.Compiled));
    private static readonly Lazy<Regex> CompiledNestedArray = new(static () => new Regex(NestedArrayPattern, RegexOptions.Compiled));

    // Escape config values before embedding in regex patterns (prevents ReDoS)
    private static string Esc(string s) => Regex.Escape(s);
    private static string EscJoin(IEnumerable<string> values) => string.Join("|", values.Select(Regex.Escape));

    public static void ValidateConfiguration()
    {
        if (string.IsNullOrWhiteSpace(EngineDefaults.Config.Negation))
            throw new InvalidOperationException("Pagin8 configuration error: Negation cannot be empty.");

        if (EngineDefaults.Config.ComparisonOperators.Any(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException("Pagin8 configuration error: ComparisonOperators cannot contain empty values.");

        if (EngineDefaults.Config.DateRangeOperators.Any(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException("Pagin8 configuration error: DateRangeOperators cannot contain empty values.");

        if (EngineDefaults.Config.GroupOperators.Any(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException("Pagin8 configuration error: GroupOperators cannot contain empty values.");
    }

    public static string DateRangePattern => $@"^(?<field>\w+)=(?<negation>{Esc(EngineDefaults.Config.Negation)}\.)?(?<operator>{EscJoin(EngineDefaults.Config.DateRangeOperators)})\.(?<value>\d+)(?<range>[dmwy])(?<exact>e)?(?<strict>s)?(?:\^(?<comment>.+))?$";

    public static string NestedDateRangePattern => $@"^(?<field>\w+)\.(?<negation>{Esc(EngineDefaults.Config.Negation)}\.)?(?<operator>{EscJoin(EngineDefaults.Config.DateRangeOperators)})\.(?<value>\d+)(?<range>[dmwy])(?<exact>e)?(?<strict>s)?(?:\^(?<comment>.+))?$";

    public static string ComparisonPattern => $@"^(?<field>[^.]+)=(?<negation>{Esc(EngineDefaults.Config.Negation)}\.)?(?<operator>({EscJoin(EngineDefaults.Config.ComparisonOperators)}))\.(?<val>.*?)(?:\^(?<comment>.+))?$";

    public static string NestedComparisonPattern => $@"^(?<field>[^.]+)\.(?<negation>{Esc(EngineDefaults.Config.Negation)}\.)?(?<operator>({EscJoin(EngineDefaults.Config.ComparisonOperators)}))\.(?<val>.*?)(?:\^(?<comment>.+))?$";

    public static string IsPattern => $@"^(?<field>[^.]+)=(?<operator>{Esc(EngineDefaults.Config.IsOperator)})\.(?<negation>{Esc(EngineDefaults.Config.Negation)}\.)?(?<val>.*?)(?:\^(?<comment>.+))?$";

    public static string NestedIsPattern => $@"^(?<field>[^.]+)\.(?<operator>{Esc(EngineDefaults.Config.IsOperator)})\.(?<negation>{Esc(EngineDefaults.Config.Negation)}\.)?(?<val>.*?)(?:\^(?<comment>.+))?$";

    public static string InPattern => $@"^(?<field>[^.=]+)=(?:(?<negation>{Esc(EngineDefaults.Config.Negation)})\.)?(?:(?<comparison>eq|is|gt|gte|lt|lte|stw|enw|like|cs)\.)?(?<operator>{Esc(EngineDefaults.Config.InOperator)})\.(?<values>\(([^)]+)\)|[^)]+)(?:\^(?<comment>.+))?$";

    public static string NestedInPattern => $@"^(?<field>[^.=]+)(?:\.(?<negation>{Esc(EngineDefaults.Config.Negation)}))?(?:\.(?<comparison>eq|is|gt|gte|lt|lte|stw|enw|like|cs))?\.(?<operator>{Esc(EngineDefaults.Config.InOperator)})\.(?<values>\(([^)]+)\)|[^)]+)(?:\^(?<comment>.+))?$";

    public static string GroupingPattern => $@"^(?<negation>{Esc(EngineDefaults.Config.Negation)}\.)?(?<operator>{EscJoin(EngineDefaults.Config.GroupOperators)})=\((?<val>.*)\)(?:\^(?<comment>.+))?$";

    public static string NestedGroupingPattern => $@"^(?<negation>{Esc(EngineDefaults.Config.Negation)}\.)?(?<operator>{EscJoin(EngineDefaults.Config.GroupOperators)})\((?<val>.*)\)(?:\^(?<comment>.+))?$";

    public static string SortExpressionPlainPattern => $@"(?<field>\$?\w+)\.(?<order>{EscJoin(EngineDefaults.Config.PossibleOrder)})";

    public static string SortExpressionPagingPattern => $@"(?<field>\$?\w+)\.(?<order>{EscJoin(EngineDefaults.Config.PossibleOrder)})\.(?<lastValue>.*?)(?=(\.\w+\.)|(?:,\w+\.)|$)";

    public static string PagingSectionPattern => @"paging=\((.*)\)";

    public static string NestedFilterPattern => @"^(?<field>\w+)\.with=\((?<conditions>[\s\S]*)\)(?:\^(?<comment>.+))?$";
    public static string NestedNestedFilterPattern => @"^(?<field>\w+)\.with.\((?<conditions>[\s\S]*)\)(?:\^(?<comment>.+))?$";

    public static string ArrayPattern => $@"^(?<field>[^.=]+)=(?:(?<negation>{Esc(EngineDefaults.Config.Negation)})\.)?(?<mode>incl|excl)\((?<values>.*)\)(?:\^(?<comment>.+))?$";

    public static string NestedArrayPattern => $@"^(?<field>[^.=]+)(?:\.(?<negation>{Esc(EngineDefaults.Config.Negation)}))?\.(?<mode>incl|excl)\((?<values>.*)\)(?:\^(?<comment>.+))?$";

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
                    if (stack.Count == 0)
                        throw new Pagin8Exception(Pagin8StatusCode.Pagin8_MalformedQuery.Code);
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

        return parts;
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

    public static string NormalizeValue(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return raw;

        if (raw.StartsWith("(") && raw.EndsWith(")"))
        {
            if (raw.Length < 2)
                return string.Empty;
            return raw.Substring(1, raw.Length - 2).Trim();
        }

        return raw;
    }

    public static bool IsComparisonOperation(string query) => CompiledComparison.Value.IsMatch(query) || CompiledNestedComparison.Value.IsMatch(query);

    public static bool IsIsOperation(string query) => CompiledIs.Value.IsMatch(query) || CompiledNestedIs.Value.IsMatch(query);

    public static bool IsInOperation(string query) => CompiledIn.Value.IsMatch(query) || CompiledNestedIn.Value.IsMatch(query);

    public static bool IsGroupingOperation(string query) => CompiledGrouping.Value.IsMatch(query) || CompiledNestedGrouping.Value.IsMatch(query);

    public static bool IsSortOperation(string query) => query.StartsWith("sort(", StringComparison.OrdinalIgnoreCase);

    public static bool IsLimitOperation(string query) => query.StartsWith("limit.", StringComparison.OrdinalIgnoreCase);

    public static bool IsSelectOperation(string query) => query.StartsWith("select=", StringComparison.OrdinalIgnoreCase);

    public static bool IsPagingOperation(string query) => query.StartsWith("paging=", StringComparison.OrdinalIgnoreCase);

    public static bool IsCountOperation(string query) => query.StartsWith("count.", StringComparison.OrdinalIgnoreCase);

    public static bool IsDateRangeOperation(string query) => CompiledDateRange.Value.IsMatch(query) || CompiledNestedDateRange.Value.IsMatch(query);

    public static bool IsMetadataOperation(string query) => query.StartsWith("metaInclude=", StringComparison.OrdinalIgnoreCase);

    public static bool IsNestedFilterOperation(string query) => CompiledNestedFilter.Value.IsMatch(query) || CompiledNestedNestedFilter.Value.IsMatch(query);

    public static bool IsArrayOperation(string query) => CompiledArray.Value.IsMatch(query) || CompiledNestedArray.Value.IsMatch(query);
}
