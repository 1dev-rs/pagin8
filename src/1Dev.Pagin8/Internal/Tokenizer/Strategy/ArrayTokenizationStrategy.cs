using System.Text.RegularExpressions;
using _1Dev.Pagin8.Internal.Exceptions.Base;
using _1Dev.Pagin8.Internal.Exceptions.StatusCodes;
using _1Dev.Pagin8.Internal.Helpers;
using _1Dev.Pagin8.Internal.Tokenizer.Operators;
using _1Dev.Pagin8.Internal.Tokenizer.Tokens;
using _1Dev.Pagin8.Internal.Validators;

namespace _1Dev.Pagin8.Internal.Tokenizer.Strategy;

public class ArrayTokenizationStrategy : ITokenizationStrategy
{
    public List<Token> Tokenize(string query, int nestingLevel = 1)
    {
        TokenValidator.ValidateNesting(nestingLevel);

        var pattern = nestingLevel == 1
            ? TokenHelper.ArrayPattern
            : TokenHelper.NestedArrayPattern;

        var match = Regex.Match(query, pattern);
        if (!match.Success) throw new Pagin8Exception(Pagin8StatusCode.Pagin8_InvalidComparison.Code);

        var field = match.Groups["field"].Value;
        var operation = match.Groups["mode"].Value.GetArrayOperator();
        var values = match.Groups["values"].Value.Split(',').Select(v => v.Trim()).ToList();
        var isNegated = !string.IsNullOrEmpty(match.Groups["negation"].Value);
        var comment = match.Groups["comment"].Success
            ? match.Groups["comment"].Value.Trim()
            : null;

        var arrayToken =  new ArrayOperationToken(field, values, operation, nestingLevel, isNegated, comment);

        return [arrayToken];
    }

    public List<Token> Tokenize(string query, string jsonPath, int nestingLevel = 1)
    {
        TokenValidator.ValidateNesting(nestingLevel);

        var pattern = nestingLevel == 1
            ? TokenHelper.InPattern
            : TokenHelper.NestedInPattern;

        var match = Regex.Match(query, pattern);
        if (!match.Success) throw new Pagin8Exception(Pagin8StatusCode.Pagin8_InvalidComparison.Code);

        var field = match.Groups["field"].Value;
        var operation = match.Groups["mode"].Value.GetArrayOperator();
        var values = match.Groups["values"].Value.Split(',').Select(v => v.Trim()).ToList();
        var isNegated = !string.IsNullOrEmpty(match.Groups["negation"].Value);
        var comment = match.Groups["comment"].Success
            ? match.Groups["comment"].Value.Trim()
            : null;

        var arrayToken = new ArrayOperationToken(field, values, operation, nestingLevel, isNegated, comment)
        {
            JsonPath = jsonPath
        };

        return [arrayToken];
    }
}
