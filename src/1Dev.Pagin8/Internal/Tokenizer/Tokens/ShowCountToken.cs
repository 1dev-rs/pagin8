using _1Dev.Pagin8.Internal.Tokenizer.Contracts;

namespace _1Dev.Pagin8.Internal.Tokenizer.Tokens;

public class ShowCountToken(bool show) : BooleanToken(show)
{
    public override QueryBuilderResult Accept<T>(ISqlTokenVisitor visitor, QueryBuilderResult result)
    {
        return visitor.Visit<T>(this, result);
    }

    public override IQueryable<T> Accept<T>(ILinqTokenVisitor<T> visitor, IQueryable<T> source) => visitor.Visit(this, source);

    public override string RevertToQueryString() => $"count.{Value.ToString().ToLower()}";
}