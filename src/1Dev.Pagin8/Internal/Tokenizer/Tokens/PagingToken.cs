using System.Text;
using _1Dev.Pagin8.Internal.Helpers;
using _1Dev.Pagin8.Internal.Tokenizer.Contracts;
using _1Dev.Pagin8.Internal.Tokenizer.Tokens.Sort;

namespace _1Dev.Pagin8.Internal.Tokenizer.Tokens;

public class PagingToken(SortToken sort, LimitToken limit, ShowCountToken showCount) : Token
{
    public override QueryBuilderResult Accept<T>(ISqlTokenVisitor visitor, QueryBuilderResult result)
    {
        return visitor.Visit<T>(this, result);
    }

    public override string RevertToQueryString()
    {
        var sb = new StringBuilder();
        sb.Append("paging=(");

        AppendTokenQueryString(sb, Sort);
        AppendTokenQueryString(sb, Limit);
        AppendTokenQueryString(sb, Count);

        TokenHelper.RemoveTrailingComma(sb);

        sb.Append(')');

        return sb.ToString();
    }

    public SortToken Sort { get; set; } = sort;

    public LimitToken Limit { get; set; } = limit;

    public ShowCountToken Count { get; set; } = showCount;

    private static void AppendTokenQueryString(StringBuilder sb, Token token)
    {
        if (token == null) return;
        sb.Append(token.RevertToQueryString());
        sb.Append(',');
    }

    public override IQueryable<T> Accept<T>(ILinqTokenVisitor<T> visitor, IQueryable<T> source) => visitor.Visit(this, source);
}