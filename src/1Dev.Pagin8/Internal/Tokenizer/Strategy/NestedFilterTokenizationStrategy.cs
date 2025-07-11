using System.Text.RegularExpressions;
using _1Dev.Pagin8.Internal.Exceptions.Base;
using _1Dev.Pagin8.Internal.Exceptions.StatusCodes;
using _1Dev.Pagin8.Internal.Helpers;
using _1Dev.Pagin8.Internal.Tokenizer.Contracts;
using _1Dev.Pagin8.Internal.Tokenizer.Tokens;
using _1Dev.Pagin8.Internal.Validators;

namespace _1Dev.Pagin8.Internal.Tokenizer.Strategy;

public class NestedFilterTokenizationStrategy(ITokenizer tokenizer) : ITokenizationStrategy
{
    public List<Token> Tokenize(string query, int nestingLevel = 1)
    {
        TokenValidator.ValidateNesting(nestingLevel);

        var pattern = nestingLevel == 1
            ? TokenHelper.NestedFilterPattern
            : TokenHelper.NestedNestedFilterPattern;

        var match = Regex.Match(query, pattern);
        if (!match.Success) throw new Pagin8Exception(Pagin8StatusCode.Pagin8_InvalidComparison.Code);

        var field = match.Groups["field"].Value;
        var conditions = match.Groups["conditions"].Value;

        var innerNesting = nestingLevel + 1;

        var innerTokens = tokenizer.Tokenize(conditions, innerNesting);

        foreach (var innerToken in innerTokens)
        {
            innerToken.JsonPath = field;
        }
        var nestedToken = new NestedFilterToken(field, innerTokens);

        return [nestedToken];
    }

    public List<Token> Tokenize(string query, string jsonPath, int nestingLevel = 1)
    {
        throw new NotImplementedException();
    }
}
