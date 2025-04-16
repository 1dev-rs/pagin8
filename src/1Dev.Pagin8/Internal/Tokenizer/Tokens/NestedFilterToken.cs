using System.Text;
using _1Dev.Pagin8.Internal.Tokenizer.Contracts;

namespace _1Dev.Pagin8.Internal.Tokenizer.Tokens;

public class NestedFilterToken(string field, List<Token> tokens) : FilterToken
{
    public override QueryBuilderResult Accept<T>(ISqlTokenVisitor visitor, QueryBuilderResult result) => visitor.Visit<T>(this, result);

    public override string RevertToQueryString()
    {
        var sb = new StringBuilder();

        sb.Append($"{Field}.with=(");

        var nestedTokens = Tokens.Select(token => token.RevertToQueryString());

        sb.Append(string.Join(',', nestedTokens));

        sb.Append(')');

        return sb.ToString();
    }

    public override IQueryable<T> Accept<T>(ILinqTokenVisitor<T> visitor, IQueryable<T> source) => visitor.Visit(this, source);

    public string Field { get; set; } = field;

    public List<Token> Tokens { get; } = tokens;
}