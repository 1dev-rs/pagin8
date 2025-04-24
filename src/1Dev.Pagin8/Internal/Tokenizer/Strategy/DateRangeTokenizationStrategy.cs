using System.Text.RegularExpressions;
using _1Dev.Pagin8.Internal.Exceptions.Base;
using _1Dev.Pagin8.Internal.Exceptions.StatusCodes;
using _1Dev.Pagin8.Internal.Helpers;
using _1Dev.Pagin8.Internal.Tokenizer.Operators;
using _1Dev.Pagin8.Internal.Tokenizer.Tokens;
using _1Dev.Pagin8.Internal.Validators;

namespace _1Dev.Pagin8.Internal.Tokenizer.Strategy;

public class DateRangeTokenizationStrategy : ITokenizationStrategy
{
    public List<Token> Tokenize(string query, int nestingLevel = 1)
    {
        TokenValidator.ValidateNesting(nestingLevel);

        var tokens = new List<Token>();
        var match = nestingLevel == 1 ? 
            Regex.Match(query, TokenHelper.DateRangePattern) : 
            Regex.Match(query, TokenHelper.NestedDateRangePattern);

        if (match.Success)
        {
            var field = match.Groups["field"].Value;
            var isNegated = !string.IsNullOrEmpty(match.Groups["negation"].Value);
            var operatorString = match.Groups["operator"].Value;
            var @operator = operatorString.GetDateRangeOperator();
            var value = int.Parse(match.Groups["value"].Value);
            var range = match.Groups["range"].Value[0].GetDateRange();
            var exact = !string.IsNullOrEmpty(match.Groups["exact"].Value);
            var strict = !string.IsNullOrEmpty(match.Groups["strict"].Value);
            var comment = match.Groups["comment"].Success
                ? match.Groups["comment"].Value.Trim()
                : null;

            var dateToken = new DateRangeToken(field, @operator, value, range, exact, strict, nestingLevel, isNegated, comment);
            tokens.Add(dateToken);
        }
        else
        {
            throw new Pagin8Exception(Pagin8StatusCode.Pagin8_InvalidDateRange.Code);
        }

        return tokens;
    }

    public List<Token> Tokenize(string query, string jsonPath, int nestingLevel = 1)
    {
        throw new NotImplementedException();
    }
}