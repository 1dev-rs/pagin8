using System.Text.RegularExpressions;
using _1Dev.Pagin8.Internal.Exceptions.Base;
using _1Dev.Pagin8.Internal.Exceptions.StatusCodes;
using _1Dev.Pagin8.Internal.Helpers;
using _1Dev.Pagin8.Internal.Tokenizer.Tokens;
using _1Dev.Pagin8.Internal.Validators;

namespace _1Dev.Pagin8.Internal.Tokenizer.Strategy;

public class IsTokenizationStrategy : ITokenizationStrategy
{
    private const string EmptyPlaceholder = "$empty";

    public List<Token> Tokenize(string query, int nestingLevel = 1)
    {
        TokenValidator.ValidateNesting(nestingLevel);

        var pattern = nestingLevel == 1
            ? TokenHelper.IsPattern
            : TokenHelper.NestedIsPattern;

        var match = Regex.Match(query, pattern);

        if (!match.Success) throw new Pagin8Exception(Pagin8StatusCode.Pagin8_InvalidComparison.Code);

        var field = match.Groups["field"].Value;
        var value = match.Groups["val"].Value;
        var comment = match.Groups["comment"].Success
            ? match.Groups["comment"].Value.Trim()
            : null;

        ValidateValue(value, out var isEmptyQuery);
        var isNegated = !string.IsNullOrEmpty(match.Groups["negation"].Value);

        var isToken = new IsToken(field, value, nestingLevel, isNegated, isEmptyQuery, comment);

        return [isToken];
    }

    public List<Token> Tokenize(string query, string jsonPath, int nestingLevel = 1)
    {
        throw new NotImplementedException();
    }

    private static void ValidateValue(string value, out bool isEmptyQuery)
    {
        if (string.IsNullOrEmpty(value))
        {
            throw new Pagin8Exception(Pagin8StatusCode.Pagin8_InvalidIsToken.Code);
        }

        if (bool.TryParse(value, out _))
        {
            isEmptyQuery = false;
        }
        else if (value.Equals(EmptyPlaceholder, StringComparison.InvariantCultureIgnoreCase))
        {
            isEmptyQuery = true;
        }
        else
        {
            throw new Pagin8Exception(Pagin8StatusCode.Pagin8_InvalidIsToken.Code);
        }
    }
}
