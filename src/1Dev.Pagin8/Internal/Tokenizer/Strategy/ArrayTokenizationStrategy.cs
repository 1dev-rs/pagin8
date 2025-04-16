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

        var match = Regex.Match(query, TokenHelper.ArraySectionPattern);
        if (!match.Success) throw new Pagin8Exception(Pagin8StatusCode.Pagin8_InvalidComparison.Code);

        var field = match.Groups[1].Value;
        var operation = match.Groups[2].Value.Equals("incl") ? ArrayOperationType.Include : ArrayOperationType.Exclude;
        var values = match.Groups[3].Value.Split(',').Select(v => v.Trim()).ToList();

        var arrayToken =  new ArrayOperationToken(field, values, operation);

        return [arrayToken];
    }

    public List<Token> Tokenize(string query, string jsonPath, int nestingLevel = 1)
    {
        TokenValidator.ValidateNesting(nestingLevel);

        var match = Regex.Match(query, TokenHelper.ArraySectionPattern);
        if (!match.Success) throw new Pagin8Exception(Pagin8StatusCode.Pagin8_InvalidComparison.Code);

        var field = match.Groups[1].Value;
        var operation = match.Groups[2].Value.Equals("incl") ? ArrayOperationType.Include : ArrayOperationType.Exclude;
        var values = match.Groups[3].Value.Split(',').Select(v => v.Trim()).ToList();

        var arrayToken = new ArrayOperationToken(field, values, operation)
        {
            JsonPath = field
        };

        return [arrayToken];
    }
}
