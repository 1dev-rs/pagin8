using System.Text.RegularExpressions;
using _1Dev.Pagin8.Internal.Exceptions.Base;
using _1Dev.Pagin8.Internal.Exceptions.StatusCodes;
using _1Dev.Pagin8.Internal.Helpers;
using _1Dev.Pagin8.Internal.Tokenizer.Operators;
using _1Dev.Pagin8.Internal.Tokenizer.Tokens;
using _1Dev.Pagin8.Internal.Validators;

namespace _1Dev.Pagin8.Internal.Tokenizer.Strategy;

public class ComparisonTokenizationStrategy : ITokenizationStrategy
{
   public List<Token> Tokenize(string query, int nestingLevel = 1)
    {
        TokenValidator.ValidateNesting(nestingLevel);

        var pattern = nestingLevel == 1
            ? TokenHelper.ComparisonPattern
            : TokenHelper.NestedComparisonPattern;

        var match = Regex.Match(query, pattern);

        if (!match.Success) throw new Pagin8Exception(Pagin8StatusCode.Pagin8_InvalidComparison.Code);

        var field = match.Groups["field"].Value;
        var @operator = match.Groups["operator"].Value;
        var value = match.Groups["val"].Value;
        var isNegated = !string.IsNullOrEmpty(match.Groups["negation"].Value);
        var comment = match.Groups["comment"].Success
            ? match.Groups["comment"].Value.Trim()
            : null;

        var comparisonToken = new ComparisonToken(field, @operator.GetComparisonOperator(), value, nestingLevel, isNegated, comment);

        return [comparisonToken];
    }

    public List<Token> Tokenize(string query, string jsonPath, int nestingLevel = 1)
    {
        TokenValidator.ValidateNesting(nestingLevel);

        var pattern = nestingLevel == 1
            ? TokenHelper.ComparisonPattern
            : TokenHelper.NestedComparisonPattern;

        var match = Regex.Match(query, pattern);

        if (!match.Success) throw new Pagin8Exception(Pagin8StatusCode.Pagin8_InvalidComparison.Code);

        var field = match.Groups["field"].Value;
        var @operator = match.Groups["operator"].Value;
        var rawValue = match.Groups["val"].Value;
        var value = TokenHelper.NormalizeValue(rawValue);
        var isNegated = !string.IsNullOrEmpty(match.Groups["negation"].Value);
        var comment = match.Groups["comment"].Success
            ? match.Groups["comment"].Value.Trim()
            : null;

        var comparisonToken = new ComparisonToken(field, @operator.GetComparisonOperator(), value, nestingLevel, isNegated, comment, jsonPath);

        return [comparisonToken];
    }
}
