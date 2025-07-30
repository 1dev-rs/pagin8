using System.Text.RegularExpressions;
using _1Dev.Pagin8.Internal.Exceptions.Base;
using _1Dev.Pagin8.Internal.Exceptions.StatusCodes;
using _1Dev.Pagin8.Internal.Helpers;
using _1Dev.Pagin8.Internal.Tokenizer.Operators;
using _1Dev.Pagin8.Internal.Tokenizer.Tokens;
using _1Dev.Pagin8.Internal.Validators;

namespace _1Dev.Pagin8.Internal.Tokenizer.Strategy;

public class InTokenizationStrategy : ITokenizationStrategy
{
    public List<Token> Tokenize(string query, int nestingLevel = 1)
    {
        TokenValidator.ValidateNesting(nestingLevel);

        var tokens = new List<Token>();

        var pattern = nestingLevel == 1
            ? TokenHelper.InPattern
            : TokenHelper.NestedInPattern;

        var match = Regex.Match(query, pattern);

        if (!match.Success) throw new Pagin8Exception(Pagin8StatusCode.Pagin8_InvalidIn.Code);

        var field = match.Groups["field"].Value;
        var @operator = match.Groups["operator"].Value;
        var value = match.Groups["values"].Value;
        var comparison = match.Groups["comparison"].Value;
        var isNegated = !string.IsNullOrEmpty(match.Groups["negation"].Value);
        var comment = match.Groups["comment"].Success
            ? match.Groups["comment"].Value.Trim()
            : null;

        if (string.IsNullOrEmpty(field) || string.IsNullOrEmpty(@operator) || string.IsNullOrEmpty(value))
        {
            throw new Pagin8Exception(Pagin8StatusCode.Pagin8_InvalidIn.Code);
        }

        TokenValidator.ValidateComparison(@operator);

        var inToken = !string.IsNullOrEmpty(comparison) ?
            new InToken(field, value, nestingLevel, comparison.GetComparisonOperator(), isNegated, comment) :
            new InToken(field, value, nestingLevel, isNegated: isNegated, comment: comment);

        tokens.Add(inToken);
        return tokens;
    }

    public List<Token> Tokenize(string query, string jsonPath, int nestingLevel = 1)
    {
        throw new NotImplementedException();
    }


}