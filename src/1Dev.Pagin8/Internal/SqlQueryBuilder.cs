using InterpolatedSql.Dapper;
using InterpolatedSql.Dapper.SqlBuilders;
using _1Dev.Pagin8.Input;
using _1Dev.Pagin8.Internal.Tokenizer;
using _1Dev.Pagin8.Internal.Tokenizer.Contracts;
using _1Dev.Pagin8.Internal.Tokenizer.Tokens;
using _1Dev.Pagin8.Internal.Tokenizer.Tokens.Sort;
using Pagin8.Internal.Configuration;
// ReSharper disable InterpolatedStringExpressionIsNotIFormattable

namespace _1Dev.Pagin8.Internal;

public class SqlQueryBuilder(ITokenizationService tokenizationService, ISqlTokenVisitor tokenVisitor) : ISqlQueryBuilder
{
    #region Public methods

    public QueryBuilderResult BuildSqlQuery<T>(QueryBuilderParameters parameters) where T : class
    {
        var input = parameters.InputParameters;

        var tokenizationResponse = tokenizationService.Tokenize<T>(input);

        var result = CreateInitialQueryResult(parameters, tokenizationResponse);

        TrySkipQueryBuilding<T>(tokenizationResponse, result);

        UpdateQueryMeta(result, tokenizationResponse);

        BuildQueryFromTokens<T>(result, tokenizationResponse.Tokens, input.IsCount);

        WrapQueryAsJsonIfNeeded(input, result);

        return result;
    }

    #endregion

    #region Private methods

    private static QueryBuilderResult CreateInitialQueryResult(QueryBuilderParameters parameters,
        TokenizationResponse tokenizationResponse)
    {
        var meta = Meta.CreateWithSanitizedQuery(tokenizationResponse.SanitizedQuery);
        return new QueryBuilderResult
        {
            Builder = new QueryBuilder(parameters.Connection, parameters.BaseQuery),
            Meta = meta
        };
    }

    private void TrySkipQueryBuilding<T>(TokenizationResponse tokenizationResponse, QueryBuilderResult result)
        where T : class
    {
        TrySkipBuildWhenOnlyCountRequested<T>(tokenizationResponse, result);
        TrySkipBuildWhenOnlyMetaRequested(tokenizationResponse, result);
    }

    private static void UpdateQueryMeta(QueryBuilderResult result, TokenizationResponse tokenizationResponse)
    {
        result.Meta = Meta.CreateWithSanitizedQuery(tokenizationResponse.SanitizedQuery);
    }

    private static void WrapQueryAsJsonIfNeeded(QueryInputParameters input, QueryBuilderResult result)
    {
        if (input.IsJson)
        {
            result.Builder = BuildJsonWrapper(result.Builder, input.CtePrefix, input.IsCount);
        }
    }

    private void TrySkipBuildWhenOnlyCountRequested<T>(TokenizationResponse tokenizationResponse,
        QueryBuilderResult result) where T : class
    {
        var pagingToken = tokenizationResponse.Tokens
            .OfType<PagingToken>()
            .FirstOrDefault();

        if (tokenizationResponse.IsCountOnly && pagingToken is not null)
        {
            ProcessCountToken<T>(pagingToken.Count, result);
        }
    }

    private static void TrySkipBuildWhenOnlyMetaRequested(TokenizationResponse tokenizationResponse,
        QueryBuilderResult result)
    {
        if (tokenizationResponse.IsMetaOnly)
        {
            result.ShouldSkipBuilder = true; // Skip fetching data
        }
    }

    private void ProcessCountToken<T>(ShowCountToken? showCount, QueryBuilderResult result) where T : class
    {
        if(showCount is null) return;
        result = tokenVisitor.Visit<T>(showCount, result);
        result.ShouldSkipBuilder = true; // Skip fetching data
    }

    private void BuildQueryFromTokens<T>(QueryBuilderResult result, IEnumerable<Token> tokens, bool isCount)
        where T : class
    {
        var tokenList = tokens.ToList();
        if (!tokenList.Any()) return;

        foreach (var token in tokenList)
        {
            if (!SkipJoinKeyword(token))
            {
                result.Builder += $"{EngineDefaults.Config.QueryJoinKeyword:raw}";
            }

            if (SkipForCount(token, isCount)) continue;

            result = token.Accept<T>(tokenVisitor, result);
        }
    }

    private static bool SkipForCount(Token token, bool isCount)
    {
        return isCount && token is PagingToken or SelectToken;
    }

    private static bool SkipJoinKeyword(Token token)
    {
        switch (token)
        {
            case PagingToken { Sort.SortExpressions: { } sortExpressions }
                when sortExpressions.All(expression => string.IsNullOrEmpty(expression.LastValue)):
            case PagingToken { Count: not null, Sort: null, Limit: null }:
            case SortToken:
            case SelectToken:
            case MetaIncludeToken:
                return true;
        }

        return false;
    }

    private static QueryBuilder BuildJsonWrapper(QueryBuilder innerQuery, string cte, bool isCount)
    {
        return innerQuery.DbConnection.QueryBuilder(isCount ? 
            $"{cte:raw}{innerQuery:raw}" : 
            (FormattableString)$"{cte:raw}SELECT COALESCE(json_agg(items), '[]') FROM ({innerQuery:raw}) items");
    }

    #endregion
}