using _1Dev.Pagin8.Internal.Exceptions.Base;
using _1Dev.Pagin8.Internal.Exceptions.StatusCodes;
using _1Dev.Pagin8.Internal.Helpers;
using _1Dev.Pagin8.Internal.Tokenizer.Tokens;

namespace _1Dev.Pagin8.Internal.Tokenizer.Strategy;

public class SelectTokenizationStrategy : ITokenizationStrategy
{
    public List<Token> Tokenize(string query, int nestingLevel = 1)
    {
        if (nestingLevel > 1)
        {
            throw new Pagin8Exception(Pagin8StatusCode.Pagin8_RootLevelOperation.Code);
        }

        var selectParts = query.Split(['='], 2);

        if (selectParts.Length != 2 || string.IsNullOrEmpty(selectParts[1]))
        {
            throw new Pagin8Exception(Pagin8StatusCode.Pagin8_InvalidSelect.Code);
        }

        var selectFields = selectParts[1].Split(',').Select(x => x.Trim()).ToList();

        var selectToken = new SelectToken(selectFields);

        return [selectToken];
    }

    public List<Token> Tokenize(string query, string jsonPath, int nestingLevel = 1)
    {
        throw new System.NotImplementedException();
    }

    public static SelectToken Default => new([QueryConstants.SelectAsterisk]);
}