using System.Collections;
using AspNet.Transliterator;
using _1Dev.Pagin8.Internal.Tokenizer.Operators;

namespace _1Dev.Pagin8.Internal.Helpers;
public static class QueryBuilderHelper
{
    public const string EscapeCharacter = "\\";

    private static Func<string, string> GetComparisonOperatorNpgSqlFormatMap(ComparisonOperator op)
    {
        return op switch
        {
            ComparisonOperator.StartsWith => value => $"{value.EscapeSpecialCharacters()}%",
            ComparisonOperator.EndsWith => value => $"%{value.EscapeSpecialCharacters()}",
            ComparisonOperator.Contains => value => $"%{value.EscapeSpecialCharacters()}%",
            _ => value => value
        };
    }

    public static string MapComparisonToNpgsqlString(ComparisonOperator comparisonOperator, string value, bool isTranslit, bool isSort)
    {
        if (isSort) return value;

        value = Transliteration.ToLowerBoldLatin(value);

        var formatFunc = GetComparisonOperatorNpgSqlFormatMap(comparisonOperator);

        return formatFunc(value);
    }

    public static bool HasNextToken(int index, ICollection tokens)
    {
        return index < tokens.Count - 1;
    }

    private static string EscapeSpecialCharacters(this string input)
    {
        return input
            .Replace(EscapeCharacter, EscapeCharacter + EscapeCharacter)
            .Replace("_", EscapeCharacter + "_")
            .Replace("%", EscapeCharacter + "%");
    }
}