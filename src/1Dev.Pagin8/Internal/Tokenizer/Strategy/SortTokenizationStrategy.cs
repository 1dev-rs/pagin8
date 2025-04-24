using System.Text.RegularExpressions;
using _1Dev.Pagin8.Internal.Exceptions.Base;
using _1Dev.Pagin8.Internal.Exceptions.StatusCodes;
using _1Dev.Pagin8.Internal.Helpers;
using _1Dev.Pagin8.Internal.Tokenizer.Operators;
using _1Dev.Pagin8.Internal.Tokenizer.Tokens;
using _1Dev.Pagin8.Internal.Tokenizer.Tokens.Sort;

namespace _1Dev.Pagin8.Internal.Tokenizer.Strategy;

public class SortTokenizationStrategy : ITokenizationStrategy
{
    public List<Token> Tokenize(string query, int nestingLevel = 1)
    {
        if (nestingLevel == 1)
        {
            throw new Pagin8Exception(Pagin8StatusCode.Pagin8_RootLevelOperation.Code);
        }

        var sortSectionRegex = new Regex(@"sort\(([^)]+)\)");
        var sortSectionMatch = sortSectionRegex.Match(query);

        if (!sortSectionMatch.Success)
        {
            throw new Pagin8Exception(Pagin8StatusCode.Pagin8_SortSectionMissing.Code);
        }

        var sortSection = sortSectionMatch.Groups[1].Value;

        var sortToken = HandleSort(sortSection);

        return [sortToken];
    }

    public List<Token> Tokenize(string query, string jsonPath, int nestingLevel = 1)
    {
        throw new NotImplementedException();
    }

    private static Token HandleSort(string expressions)
    {
        var isPagingExpression = Regex.IsMatch(expressions, TokenHelper.SortExpressionPagingPattern);
        var isPlainExpression = Regex.IsMatch(expressions, TokenHelper.SortExpressionPlainPattern);

        if (!isPlainExpression && !isPagingExpression)
        {
            throw new Pagin8Exception(Pagin8StatusCode.Pagin8_InvalidSortExpressionFormat.Code);
        }

        var sortExpressions = isPagingExpression
        ? ExtractPagingSortExpressions(expressions)
        : ExtractPlainSortExpressions(expressions);

        if (!KeyIsLast(sortExpressions))
        {
            throw new Pagin8Exception(Pagin8StatusCode.Pagin8_InvalidSortKeyPosition.Code);
        }

        return new SortToken(sortExpressions);
    }

    private static List<SortExpression> ExtractPagingSortExpressions(string expressions)
    {
         var lastIndex = expressions.LastIndexOf(',');
        if (!expressions.Contains(QueryConstants.KeyPlaceholder))
        {
            throw new Pagin8Exception(Pagin8StatusCode.Pagin8_SortKeyCursorMissing.Code);
        }

        if (lastIndex < 0)
        {
            throw new Pagin8Exception(Pagin8StatusCode.Pagin8_InvalidSortExpressionFormat.Code);
        }

        var fieldsOrders = expressions[..lastIndex];
        var keyOrder = expressions[(lastIndex + 1)..];

        if (!keyOrder.StartsWith(QueryConstants.KeyPlaceholder))
        {
            throw new Pagin8Exception(Pagin8StatusCode.Pagin8_InvalidSortKeyPosition.Code);
        }

        var keyMatch = Regex.Match(keyOrder, TokenHelper.SortExpressionKeyPattern);

        if (!keyMatch.Success)
        {
            throw new Pagin8Exception(Pagin8StatusCode.Pagin8_InvalidSortKayPlaceholderFormat.Code);
        }

        var matches = Regex.Matches(fieldsOrders, TokenHelper.SortExpressionPagingPattern);

        var sortExpressions = new List<SortExpression>();
        foreach (Match match in matches)
        {
            var fieldName = match.Groups["field"].Value;
            var order = match.Groups["order"].Value;
            var lastValue = match.Groups["lastValue"].Value;

            sortExpressions.Add(new SortExpression(fieldName, order.GetSortOrder(), TryProcessPlaceholders(lastValue)));
        }

        sortExpressions.Add(new SortExpression(QueryConstants.KeyPlaceholder, SortOrder.Ascending, keyMatch.Groups[1].Value));

        return sortExpressions;
    }


    private static List<SortExpression> ExtractPlainSortExpressions(string expressions)
    {
        var matches = Regex.Matches(expressions, TokenHelper.SortExpressionPlainPattern);

        var sortExpressions = new List<SortExpression>();
        foreach (Match match in matches)
        {
            var fieldName = match.Groups["field"].Value;
            var order = match.Groups["order"].Value;

            sortExpressions.Add(new SortExpression(fieldName, order.GetSortOrder()));
        }

        return sortExpressions;
    }

    private static string TryProcessPlaceholders(string? lastValue)
    {
        if (string.IsNullOrWhiteSpace(lastValue) ||
            lastValue.Equals(QueryConstants.NullPlaceholder, StringComparison.InvariantCultureIgnoreCase))
        {
            return string.Empty;
        }

        return lastValue.Equals(QueryConstants.EmptyPlaceholder, StringComparison.InvariantCultureIgnoreCase)
            ? string.Empty
            : lastValue;
    }


    private static bool KeyIsLast(IReadOnlyCollection<SortExpression> sortExpressions) => sortExpressions.All(x => string.IsNullOrEmpty(x.LastValue)) || (sortExpressions.Any() && sortExpressions.Last().Field == QueryConstants.KeyPlaceholder);
}