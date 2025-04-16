using _1Dev.Pagin8.Internal.Exceptions.Base;
using _1Dev.Pagin8.Internal.Exceptions.StatusCodes;
using _1Dev.Pagin8.Internal.Tokenizer.Tokens;

namespace _1Dev.Pagin8.Internal.Tokenizer.Strategy;

public class LimitTokenizationStrategy : ITokenizationStrategy
{
    public List<Token> Tokenize(string query, int nestingLevel = 1)
    {
        if (nestingLevel == 1)
        {
            throw new Pagin8Exception(Pagin8StatusCode.Pagin8_NestedLevelOperation.Code);
        }

        var limitParts = query.Split('.');

        if (limitParts.Length != 2 || !int.TryParse(limitParts[1], out var val))
        {
            throw new Pagin8Exception(Pagin8StatusCode.Pagin8_InvalidLimit.Code);
        }

        var perPageToken = new LimitToken(val);

        return [perPageToken];
    }

    public List<Token> Tokenize(string query, string jsonPath, int nestingLevel = 1)
    {
        throw new System.NotImplementedException();
    }
}