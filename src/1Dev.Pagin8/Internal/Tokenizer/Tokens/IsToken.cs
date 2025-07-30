using System.Text;
using _1Dev.Pagin8.Internal.Tokenizer.Contracts;
using _1Dev.Pagin8.Internal.Tokenizer.Operators;
using Pagin8.Internal.Configuration;

namespace _1Dev.Pagin8.Internal.Tokenizer.Tokens;

public class IsToken : FilterToken, INegationAware
{
    public IsToken(string field, string value, int nestingLevel, bool isNegated = false, bool isEmptyQuery = false, string? comment = null)
    {
        Field = field;
        Value = value;
        IsNegated = isNegated;
        IsEmptyQuery = isEmptyQuery;
        NestingLevel = nestingLevel;
        Comment = comment;
    }

    public override QueryBuilderResult Accept<T>(ISqlTokenVisitor visitor, QueryBuilderResult result) => visitor.Visit<T>(this, result);

    public override string RevertToQueryString()
    {
        var sb = new StringBuilder(Field);

        sb.Append(NestingLevel == 1 ? '=' : '.');
        sb.Append(EngineDefaults.Config.IsOperator);
        sb.Append('.');

        if (IsNegated)
        {
            var negation = EngineDefaults.Config.Negation;
            sb.Append($"{negation}.");
        }

        sb.Append(Value);

        if (!string.IsNullOrWhiteSpace(Comment))
        {
            sb.Append('^').Append(Comment);
        }

        return sb.ToString();
    }

    public override IQueryable<T> Accept<T>(ILinqTokenVisitor<T> visitor, IQueryable<T> source) => visitor.Visit(this, source);

    public string Field { get; }

    public string Value { get; }

    public bool IsNegated { get; }

    public bool IsEmptyQuery { get; }

    public int NestingLevel { get; }
}