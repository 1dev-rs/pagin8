using System.Text.RegularExpressions;
using _1Dev.Pagin8.Internal.Exceptions.Base;
using _1Dev.Pagin8.Internal.Exceptions.StatusCodes;
using _1Dev.Pagin8.Internal.Helpers;
using _1Dev.Pagin8.Internal.Tokenizer.Contracts;
using _1Dev.Pagin8.Internal.Tokenizer.Operators;
using _1Dev.Pagin8.Internal.Tokenizer.Tokens;
using _1Dev.Pagin8.Internal.Validators;

namespace _1Dev.Pagin8.Internal.Tokenizer.Strategy;

public class GroupTokenizationStrategy(ITokenizer tokenizer) : ITokenizationStrategy
{
    public List<Token> Tokenize(string query, int nestingLevel = 1)
    {
        TokenValidator.ValidateNesting(nestingLevel);

        var pattern = nestingLevel == 1
            ? TokenHelper.GroupingPattern
            : TokenHelper.NestedGroupingPattern;

        var match = Regex.Match(query, pattern);

        if (!match.Success) throw new Pagin8Exception(Pagin8StatusCode.Pagin8_InvalidGroup.Code);

        var @operator = match.Groups["operator"].Value;
        var innerGroup = match.Groups["val"].Value;
        var isNegated = !string.IsNullOrEmpty(match.Groups["negation"].Value);
        var comment = match.Groups["comment"].Success
            ? match.Groups["comment"].Value.Trim()
            : null;

        var innerNesting = nestingLevel + 1;

        var groupToken = new GroupToken(@operator.GetNestingOperator(), tokenizer.Tokenize(innerGroup, innerNesting), nestingLevel, comment, isNegated);

        return [groupToken];
    }

    public List<Token> Tokenize(string query, string jsonPath, int nestingLevel = 1)
    {
        throw new System.NotImplementedException();
    }
}