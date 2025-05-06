using _1Dev.Pagin8.Extensions;
using _1Dev.Pagin8.Input;
using _1Dev.Pagin8.Internal.Exceptions.Base;
using _1Dev.Pagin8.Internal.Exceptions.StatusCodes;
using _1Dev.Pagin8.Internal.Helpers;
using _1Dev.Pagin8.Internal.Tokenizer.Contracts;
using _1Dev.Pagin8.Internal.Tokenizer.Strategy;
using _1Dev.Pagin8.Internal.Tokenizer.Tokens;
using _1Dev.Pagin8.Internal.Tokenizer.Tokens.Sort;
using _1Dev.Pagin8.Internal.Validators;
using _1Dev.Pagin8.Internal.Validators.Contracts;

namespace _1Dev.Pagin8.Internal.Tokenizer;

public class TokenizationService(
    ITokenizer tokenizer,
    IContextValidator contextValidator,
    IPagin8MetadataProvider metadataProvider)
    : ITokenizationService
{
    
    #region Public methods
    public TokenizationResponse Tokenize<T>(QueryInputParameters input, bool validateContext = true) where T : class
    {
        var tokens = tokenizer.Tokenize(input.QueryString);

        var ret = contextValidator.ValidateFilterableTokenFields<T>(tokens);
        if (validateContext && !ret)
        {
            throw new Pagin8Exception(Pagin8StatusCode.Pagin8_TokenFieldInvalid.Code);
        }
        IdentifyExceptions(tokens, input, out var isCountOnly);

        if (!input.IsDefault)
        {
            tokens = MergeWithDefaultTokens(input, tokens);
        }

        ValidateLimit(input.IgnoreLimit, tokens);
        TryEnsureSortByKey<T>(tokens);
        
        var sanitizedQuery = Standardize(tokens);
        return new TokenizationResponse(tokens, sanitizedQuery, isCountOnly);
    }

    public string Standardize<T>(string queryString, bool isDefault = false) where T : class
    {
        var input = QueryInputParameters.CreateWithQueryString(queryString);
        input.SetDefault(isDefault);
        var response = Tokenize<T>(input, validateContext: false);
        return response.SanitizedQuery;
    }

    public string Standardize(IEnumerable<Token> tokens)
    {
        var orderedTokens = EnsureOrderByPriority(tokens).ToList();
        return tokenizer.RevertToQueryString(orderedTokens);
    }
    #endregion

    #region Private methods
    private static IEnumerable<Token> EnsureOrderByPriority(IEnumerable<Token> tokens) => tokens.OrderBy(GetPriority);

    private static int GetPriority(Token token)
    {
        var priority = token switch
        {
            ComparisonToken => TokenPriority.ComparisonToken,
            IsToken => TokenPriority.IsToken,
            DateRangeToken => TokenPriority.ComparisonToken,
            InToken => TokenPriority.InToken,
            GroupToken => TokenPriority.GroupToken,
            SortToken => TokenPriority.SortToken,
            SelectToken => TokenPriority.SelectToken,
            PagingToken => TokenPriority.PagingToken,
            MetaIncludeToken => TokenPriority.MetaIncludeToken,
            NestedFilterToken => TokenPriority.NestedFilterToken,
            ArrayOperationToken => TokenPriority.ArrayOperationToken,
            _ => throw new NotSupportedException($"Unsupported token type {token.GetType().Name}")
        };

        return (int)priority;
    }

    private static void ValidateLimit(bool ignoreLimit, IEnumerable<Token> tokens)
    {

        if (!ignoreLimit)
        {
            if (tokens.OfType<PagingToken>().SingleOrDefault() is { Limit: { } limitToken })
            {
                TokenValidator.ValidateMaxItemsPerPage(limitToken.Value);
            }
        }
        else
        {
            OverrideLimitWithMaxSafeItemCount(tokens);
        }
    }

    private static void OverrideLimitWithMaxSafeItemCount(IEnumerable<Token> tokens)
    {
        tokens.OfType<PagingToken>().Update(PagingTokenizationStrategy.TrySetMaxSafeItemCountForLimit);
    }

    private void TryEnsureSortByKey<T>(IReadOnlyCollection<Token> tokens) where T : class
    {
        if (!tokens.Any(x => x is PagingToken { Sort: not null })) return;

        var pagingToken = tokens.OfType<PagingToken>().SingleOrDefault();
        if (pagingToken?.Sort == null) return;

        var sortExpressions = pagingToken.Sort.SortExpressions;

        var entityKey = metadataProvider.GetEntityKey<T>();
        const string keyPlaceholder = QueryConstants.KeyPlaceholder;

        var alreadySortedByKey = sortExpressions.Any(x =>
            x.Field.Equals(entityKey, StringComparison.InvariantCultureIgnoreCase) ||
            x.Field.Equals(keyPlaceholder, StringComparison.InvariantCultureIgnoreCase));

        if (alreadySortedByKey) return;

        var defaultSort = PagingTokenizationStrategy.Default.Sort;
        if (defaultSort != null)
        {
            sortExpressions.AddRange(defaultSort.SortExpressions);
        }
    }

    private static void IdentifyExceptions(List<Token> tokens, QueryInputParameters input, out bool isCountOnly)
    {
         isCountOnly = IsCountOnly(tokens, input.IsCount);
    }

    private static bool IsCountOnly(List<Token> tokens, bool isCount)
    {
        var pagingToken = tokens
            .OfType<PagingToken>()
            .FirstOrDefault(t => t is { Sort: null, Limit: null });

        return !isCount && tokens.Count == 1 && pagingToken != null;
    }

    private List<Token> MergeWithDefaultTokens(QueryInputParameters input, List<Token> tokens)
    {
        var defaultTokens = new List<Token>();
        if (!string.IsNullOrEmpty(input.DefaultQueryString))
        {
            defaultTokens = tokenizer.Tokenize(input.DefaultQueryString);
        }
        return CompareAndMergeTokens(tokens, defaultTokens);
    }

    private static List<Token> CompareAndMergeTokens(List<Token> tokens, List<Token> defaultTokens)
    {
        //var hasOnlyMetaIncludeToken = tokens.Count == 1 && tokens.First() is MetaIncludeToken;
        var hasOnlyPagingCountToken = tokens.Count == 1 &&
                                      tokens.First() is PagingToken { Count: not null, Sort: null, Limit: null };

        if (hasOnlyPagingCountToken)
        {
            return tokens;
        }

        var userSelectTokens = GetTokensByType<SelectToken>(tokens);
        var userFilterTokens = GetTokensByType<FilterToken>(tokens);
        var userPagingTokens = GetTokensByType<PagingToken>(tokens);
        var userOtherTokens = tokens
            .Where(token => token is not SelectToken && token is not FilterToken && token is not PagingToken)
            .ToList();

        var defaultSelectTokens = GetTokensByType<SelectToken>(defaultTokens);
        var defaultFilterTokens = GetTokensByType<FilterToken>(defaultTokens);
        var defaultPagingTokens = GetTokensByType<PagingToken>(defaultTokens);

        var finalSelectTokens = userSelectTokens.Any() ? userSelectTokens : defaultSelectTokens;
        var finalFilterTokens = userFilterTokens.Any() ? userFilterTokens : defaultFilterTokens;
        var finalPagingTokens = userPagingTokens.Any() ? userPagingTokens : defaultPagingTokens;

        var finalTokens = new List<Token>();
        finalTokens.AddRange(finalSelectTokens);
        finalTokens.AddRange(finalFilterTokens);
        finalTokens.AddRange(finalPagingTokens);
        finalTokens.AddRange(userOtherTokens);

        TryAddFallbackDefaults(finalTokens);

        return finalTokens;
    }

    private static void TryAddFallbackDefaults(IList<Token> tokens)
    {
        var hasPaging = tokens.OfType<PagingToken>().Any();
        var hasSelect = tokens.OfType<SelectToken>().Any();

        if (!hasPaging)
        {
            tokens.TryAddDefault(PagingTokenizationStrategy.Default);
        }

        if (!hasSelect)
        {
            tokens.TryAddDefault(SelectTokenizationStrategy.Default);
        }
    }

    private static List<TToken> GetTokensByType<TToken>(IReadOnlyCollection<Token> tokens) where TToken : Token
    {
        return tokens.OfType<TToken>().ToList();
    }

    #endregion
}