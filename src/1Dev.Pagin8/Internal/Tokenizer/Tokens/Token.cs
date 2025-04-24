using _1Dev.Pagin8.Internal.Tokenizer.Contracts;

namespace _1Dev.Pagin8.Internal.Tokenizer.Tokens;

public abstract class Token
{
    public abstract QueryBuilderResult Accept<T>(ISqlTokenVisitor visitor, QueryBuilderResult res) where T : class;

    public abstract IQueryable<T> Accept<T>(ILinqTokenVisitor<T> visitor, IQueryable<T> source) where T : class;

    public abstract string RevertToQueryString();

    public string? JsonPath { get; set; }
}