using _1Dev.Pagin8.Internal.Helpers;
using _1Dev.Pagin8.Internal.Tokenizer.Contracts;
using _1Dev.Pagin8.Internal.Tokenizer.Strategy;
using _1Dev.Pagin8.Internal.Tokenizer.Tokens;

namespace _1Dev.Pagin8.Internal.Tokenizer;

public class Tokenizer : ITokenizer
{
    public List<Token> Tokenize(string query, int nestingLevel = 1)
    {
        if (string.IsNullOrEmpty(query)) return [];

        if (nestingLevel == 1)
        {
            query = TokenHelper.Normalize(query);
        }

        var delimiters = TokenHelper.TakeDelimiter(nestingLevel);
        var queryParts = TokenHelper.SplitAtDelimiters(query, delimiters).Where(x=> !string.IsNullOrEmpty(x)).ToArray();
        return TokenizeQueryParts(nestingLevel, queryParts);
    }

    public List<Token> Tokenize(string query, string jsonPath, int nestingLevel = 1)
    {
        query = TokenHelper.Normalize(query);

        var delimiters = TokenHelper.TakeDelimiter(nestingLevel);
        var queryParts = TokenHelper.SplitAtDelimiters(query, delimiters);

        return TokenizeJsonQueryParts(nestingLevel, jsonPath, queryParts);
    }

    public string RevertToQueryString(List<Token> tokens)
    {
        var queryStrings = tokens.Select(x => x.RevertToQueryString());
        var queryString = string.Join('&', queryStrings);
        return queryString;
    }

    private List<Token> TokenizeQueryParts(int nestingLevel, IEnumerable<string> queryParts)
    {
        var tokens = new List<Token>();
        foreach (var queryPart in queryParts)
        {
            tokens.AddRange(GetTokenizationStrategy(queryPart).Tokenize(queryPart, nestingLevel));
        }

        return tokens;
    }

    private List<Token> TokenizeJsonQueryParts(int nestingLevel, string jsonPath, IEnumerable<string> queryParts)
    {
        var tokens = new List<Token>();
        foreach (var queryPart in queryParts)
        {
            tokens.AddRange(GetTokenizationStrategy(queryPart).Tokenize(queryPart, jsonPath, nestingLevel));
        }

        return tokens;
    }

    private ITokenizationStrategy GetTokenizationStrategy(string part)
    {
        var factory = TokenizationStrategyFactory.GetFactory(part);

        return factory.CreateTokenizationStrategy(this, part);
    }
}