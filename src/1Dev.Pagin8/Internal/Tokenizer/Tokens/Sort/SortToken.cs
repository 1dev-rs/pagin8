using System.Text;
using _1Dev.Pagin8.Internal.Helpers;
using _1Dev.Pagin8.Internal.Tokenizer.Contracts;
using _1Dev.Pagin8.Internal.Tokenizer.Operators;

namespace _1Dev.Pagin8.Internal.Tokenizer.Tokens.Sort;
public class SortToken(List<SortExpression> sortExpressions) : Token
{
    public override QueryBuilderResult Accept<T>(ISqlTokenVisitor visitor, QueryBuilderResult result) => visitor.Visit<T>(this, result);

    public override string RevertToQueryString()
    {
        var sb = new StringBuilder();
        sb.Append("sort(");

        foreach (var sortExpression in SortExpressions)
        {
            sb.Append(sortExpression.Field);
            sb.Append('.');
            sb.Append(sortExpression.SortOrder.GetQueryFromSortOrder());

            if (SortExpressions.Any(x => x.Field.Equals(QueryConstants.KeyPlaceholder) && !string.IsNullOrEmpty(x.LastValue)))
            {
                sb.Append('.');
                sb.Append(SetLastValue(sortExpression.LastValue));
            }

            sb.Append(',');
        }

        TokenHelper.RemoveTrailingComma(sb);

        sb.Append(')');

        return sb.ToString();
    }

    private static string SetLastValue(string lastValue)
    {
        return lastValue switch
        {
            null => QueryConstants.NullPlaceholder,
            "" => QueryConstants.EmptyPlaceholder,
            _ => lastValue
        };
    }

    public override IQueryable<T> Accept<T>(ILinqTokenVisitor<T> visitor, IQueryable<T> source) => visitor.Visit(this, source);

    public List<SortExpression> SortExpressions { get; } = sortExpressions;
}
