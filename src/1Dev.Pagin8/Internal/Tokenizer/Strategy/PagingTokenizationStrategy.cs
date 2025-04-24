using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using _1Dev.Pagin8.Internal.Exceptions.Base;
using _1Dev.Pagin8.Internal.Exceptions.StatusCodes;
using _1Dev.Pagin8.Internal.Helpers;
using _1Dev.Pagin8.Internal.Tokenizer.Contracts;
using _1Dev.Pagin8.Internal.Tokenizer.Tokens;
using _1Dev.Pagin8.Internal.Tokenizer.Tokens.Sort;
using _1Dev.Pagin8.Internal.Validators;
using Internal.Configuration;

namespace _1Dev.Pagin8.Internal.Tokenizer.Strategy;

public class PagingTokenizationStrategy(ITokenizer tokenizer) : ITokenizationStrategy
{
    public static PagingToken Default => new(
        new SortToken([new SortExpression(QueryConstants.KeyPlaceholder, SortOrder.Ascending)]),
        new LimitToken(Pagin8Runtime.Config.PagingSettings.DefaultPerPage),
        new ShowCountToken(false));

    public List<Token> Tokenize(string query, int nestingLevel = 1)
    {
        TokenValidator.ValidateNesting(nestingLevel);

        if (nestingLevel > 1)
        {
            throw new Pagin8Exception(Pagin8StatusCode.Pagin8_RootLevelOperation.Code);
        }

        if (!TryGetInnerGroup(query, out var group))
        {
            throw new Pagin8Exception(Pagin8StatusCode.Pagin8_InvalidPaging.Code);
        }

        nestingLevel++;

        var tokens = tokenizer.Tokenize(group, nestingLevel);

        var sortToken = tokens.OfType<SortToken>().FirstOrDefault();
        var limitToken = tokens.OfType<LimitToken>().FirstOrDefault();
        var showCountToken = tokens.OfType<ShowCountToken>().FirstOrDefault();

        var pagingToken = new PagingToken(sortToken, limitToken, showCountToken);

        TryAddDefault(pagingToken);

        return [pagingToken];
    }

    public List<Token> Tokenize(string query, string jsonPath, int nestingLevel = 1)
    {
        throw new NotImplementedException();
    }

    private static bool TryGetInnerGroup(string query, [NotNullWhen(true)] out string? innerGroup)
    {
        var pagingSectionRegex = new Regex(TokenHelper.PagingSectionPattern);
        var match = pagingSectionRegex.Match(query);

        if (match.Success && !string.IsNullOrEmpty(match.Groups[1].Value))
        {
            innerGroup = match.Groups[1].Value;
            return true;
        }

        innerGroup = null;
        return false;
    }


    public static void TrySetMaxSafeItemCountForLimit(PagingToken token)
    {
        if (token is { Count: { }, Sort: null, Limit: null })
        {
            return;
        }

        token.Limit = new LimitToken(Pagin8Runtime.Config.PagingSettings.MaxSafeItemCount);
    }

    private static void TryAddDefault(PagingToken token)
    {
        if (token is { Count: { }, Sort: null, Limit: null })
        {
            return;
        }

        var defaultToken = Default;

        token.Sort ??= defaultToken.Sort;
        token.Limit ??= defaultToken.Limit;
        token.Count ??= defaultToken.Count;
    }
}