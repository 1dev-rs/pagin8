using _1Dev.Pagin8.Internal.Exceptions.Base;
using _1Dev.Pagin8.Internal.Exceptions.StatusCodes;
using _1Dev.Pagin8.Internal.Tokenizer.Tokens;

namespace _1Dev.Pagin8.Internal.Tokenizer.Strategy;

public class ShowCountTokenizationStrategy : ITokenizationStrategy
{
    public List<Token> Tokenize(string query, int nestingLevel = 1)
    {
        if (nestingLevel == 1)
        {
            throw new Pagin8Exception(Pagin8StatusCode.Pagin8_RootLevelOperation.Code);
        }

        var countParts = query.Split('.', 2);

        if (countParts.Length != 2 || !bool.TryParse(countParts[1], out var show))
        {
            throw new Pagin8Exception(Pagin8StatusCode.Pagin8_InvalidShowCount.Code);
        }

        var showCountToken = new ShowCountToken(show);

        return [showCountToken];
    }

    public List<Token> Tokenize(string query, string jsonPath, int nestingLevel = 1)
    {
        throw new NotImplementedException();
    }
}