using System.Text;
using _1Dev.Pagin8.Internal.Tokenizer.Contracts;
using _1Dev.Pagin8.Internal.Tokenizer.Operators;
using Pagin8.Internal.Configuration;

namespace _1Dev.Pagin8.Internal.Tokenizer.Tokens;

public class GroupToken : FilterToken, INegationAware
{
    public GroupToken(NestingOperator nestingOperator, List<Token> tokens, int nestingLevel, bool isNegated = false, string ? comment = null)
    {
        Tokens = tokens;
        NestingLevel = nestingLevel;
        IsNegated = isNegated;
        NestingOperator = nestingOperator;
        Comment = comment;
    }

    public override QueryBuilderResult Accept<T>(ISqlTokenVisitor visitor, QueryBuilderResult result) => visitor.Visit<T>(this, result);

    public override string RevertToQueryString()
    {
        var sb = new StringBuilder();

        var negation = EngineDefaults.Config.Negation;
        if (IsNegated)
        {
            sb.Append(negation);
            sb.Append('.');
        }

        sb.Append(NestingOperator.GetQueryFromNesting());

        sb.Append(NestingLevel == 1 ? "=(" : "(");

        var nestedTokens = Tokens.Select(token => token.RevertToQueryString());

        sb.Append(string.Join(',', nestedTokens));

        sb.Append(')');

        if (!string.IsNullOrWhiteSpace(Comment))
        {
            sb.Append('^').Append(Comment);
        }

        return sb.ToString();
    }

    public override IQueryable<T> Accept<T>(ILinqTokenVisitor<T> visitor, IQueryable<T> source) => visitor.Visit(this, source);

    public List<Token> Tokens { get; }

    public int NestingLevel { get; }

    public bool IsNegated { get; }

    public NestingOperator NestingOperator { get; }
}