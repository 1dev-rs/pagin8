using _1Dev.Pagin8.Internal.Tokenizer.Operators;
using _1Dev.Pagin8.Internal.Tokenizer.Tokens;
using System.Text;
using System.Text.RegularExpressions;

namespace _1Dev.Pagin8.Internal;

public static class DslConverter
{
    private static readonly Regex FilterLineRegex = new(
        @"^(?<field>\w+)(\s+not)?\s+(?<operator>\w+)\s+(?<value>[^\^]+?)(\s*\^(?<comment>.*))?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex GroupLineRegex = new(
        @"^(?<group>not\.\w+|\w+)(\s*\^(?<comment>.*))?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string ToCompact(string input)
    {
        var lines = input.Split('\n')
            .Select(l => (line: l.TrimEnd(), indent: l.TakeWhile(char.IsWhiteSpace).Count()))
            .Where(x => !string.IsNullOrWhiteSpace(x.line))
            .ToList();

        var sb = new StringBuilder();
        var indentStack = new Stack<int>();
        int groupDepth = 0;
        bool needsComma = false;
        bool isTopLevelGroupStarted = false;
        string? topLevelGroupComment = null;

        foreach (var (line, indent) in lines)
        {
            var trimmed = line.Trim();

            while (indentStack.Count > 0 && indent <= indentStack.Peek())
            {
                sb.Append(')');
                indentStack.Pop();
                groupDepth--;
                needsComma = true;
            }

            var groupMatch = GroupLineRegex.Match(trimmed);
            if (groupMatch.Success && (groupMatch.Groups["group"].Value.Equals("and", StringComparison.OrdinalIgnoreCase) ||
                                       groupMatch.Groups["group"].Value.Equals("or", StringComparison.OrdinalIgnoreCase) ||
                                       groupMatch.Groups["group"].Value.StartsWith("not.", StringComparison.OrdinalIgnoreCase)))
            {
                if (needsComma) sb.Append(',');

                var group = groupMatch.Groups["group"].Value.ToLowerInvariant();
                var comment = groupMatch.Groups["comment"].Success
                    ? groupMatch.Groups["comment"].Value.Trim()
                    : null;

                if (groupDepth == 0 && !isTopLevelGroupStarted)
                {
                    sb.Append(group).Append("=(");
                    isTopLevelGroupStarted = true;
                    if (!string.IsNullOrEmpty(comment)) topLevelGroupComment = comment;
                }
                else
                {
                    sb.Append(group).Append('(');
                }

                indentStack.Push(indent);
                groupDepth++;
                needsComma = false;
                continue;
            }

            var match = FilterLineRegex.Match(trimmed);
            if (match.Success)
            {
                if (needsComma) sb.Append(',');

                var field = match.Groups["field"].Value;
                var isNot = trimmed.Contains(" not ", StringComparison.OrdinalIgnoreCase);
                var op = match.Groups["operator"].Value;
                var val = match.Groups["value"].Value.Trim();
                var comment = match.Groups["comment"].Success ? $"^{match.Groups["comment"].Value.Trim()}" : "";

                var compact = groupDepth == 0
                    ? $"{field}={(isNot ? "not." : "")}{op}.{val}{comment}"
                    : $"{field}.{(isNot ? "not." : "")}{op}.{val}{comment}";

                sb.Append(compact);
                needsComma = true;
            }
        }

        while (indentStack.Count > 0)
        {
            sb.Append(')');
            indentStack.Pop();
        }

        if (!string.IsNullOrEmpty(topLevelGroupComment))
        {
            sb.Append('^').Append(topLevelGroupComment);
        }

        return sb.ToString();
    }

    public static string ToFriendly(IEnumerable<Token> tokens, int indent = 0)
    {
        var sb = new StringBuilder();

        foreach (var token in tokens)
        {
            RenderToken(sb, token, indent);
        }

        return sb.ToString().Replace("\r\n", "\n").TrimEnd();
    }

    private static void RenderToken(StringBuilder sb, Token token, int indent)
    {
        var pad = new string(' ', indent * 4);

        switch (token)
        {
            case GroupToken group:
                sb.AppendLine($"{pad}{group.NestingOperator.GetQueryFromNesting()}{(group.Comment != null ? " ^ " + group.Comment : string.Empty)}");
                foreach (var child in group.Tokens)
                {
                    RenderToken(sb, child, indent + 1);
                }
                break;

            case ComparisonToken ct:
                sb.Append(pad).Append(ct.Field);
                if (ct.IsNegated) sb.Append(" not");
                sb.Append(" ").Append(ct.Operator.GetQueryFromComparison());
                sb.Append(" ").Append(ct.Value);
                if (!string.IsNullOrWhiteSpace(ct.Comment))
                    sb.Append(" ^ ").Append(ct.Comment);
                sb.AppendLine();
                break;

            case InToken it:
                sb.Append(pad).Append(it.Field);
                if (it.IsNegated) sb.Append(" not");
                if (it.Comparison != ComparisonOperator.Equals)
                {
                    sb.Append(" ").Append(it.Comparison.GetQueryFromComparison());
                }
                sb.Append(" in (").Append(string.Join(",", it.Values)).Append(")");
                if (!string.IsNullOrWhiteSpace(it.Comment))
                    sb.Append(" ^ ").Append(it.Comment);
                sb.AppendLine();
                break;

            case IsToken ist:
                sb.Append(pad).Append(ist.Field);
                if (ist.IsNegated) sb.Append(" not");
                sb.Append(" is ").Append(ist.Value);
                if (!string.IsNullOrWhiteSpace(ist.Comment))
                    sb.Append(" ^ ").Append(ist.Comment);
                sb.AppendLine();
                break;

            case DateRangeToken dr:
                sb.Append(pad).Append(dr.Field);
                if (dr.IsNegated) sb.Append(" not");
                sb.Append(" ").Append(dr.Operator.GetQueryFromDateRange());
                sb.Append(" ").Append(dr.Value).Append(dr.Range.GetCharFromDateRange());
                if (dr.Exact) sb.Append("e");
                if (dr.Strict) sb.Append("s");
                if (!string.IsNullOrWhiteSpace(dr.Comment))
                    sb.Append(" ^ ").Append(dr.Comment);
                sb.AppendLine();
                break;
        }
    }
}