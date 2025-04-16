using System.Text.RegularExpressions;
using _1Dev.Pagin8.Internal.Exceptions.Base;
using _1Dev.Pagin8.Internal.Exceptions.StatusCodes;
using _1Dev.Pagin8.Internal.Helpers;
using _1Dev.Pagin8.Internal.Tokenizer.Tokens;

namespace _1Dev.Pagin8.Internal.Tokenizer.Strategy;

public class MetaIncludeTokenizationStrategy : ITokenizationStrategy
{
    public List<Token> Tokenize(string query, int nestingLevel = 1)
    {
        if (nestingLevel > 1)
        {
            throw new Pagin8Exception(Pagin8StatusCode.Pagin8_RootLevelOperation.Code);
        }

        var metaParts = query.Split('=', 2);

        if (metaParts.Length != 2) throw new Pagin8Exception(Pagin8StatusCode.Pagin8_InvalidMetaInclude.Code);

        var match = Regex.Match(query, TokenHelper.MetaIncludePattern, RegexOptions.IgnoreCase);

        if (!match.Success) throw new Pagin8Exception(Pagin8StatusCode.Pagin8_InvalidMetaInclude.Code);

        var filters = match.Groups["filters"].Success;
        var subscriptions = match.Groups["subscriptions"].Success;
        var columns = match.Groups["columns"].Success;
        var showMetaToken = new MetaIncludeToken(filters, subscriptions, columns);

        return [showMetaToken];
    }

    public List<Token> Tokenize(string query, string jsonPath, int nestingLevel = 1)
    {
        throw new System.NotImplementedException();
    }
}