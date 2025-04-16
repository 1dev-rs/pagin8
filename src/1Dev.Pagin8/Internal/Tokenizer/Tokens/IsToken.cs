using System.Text;
using _1Dev.Pagin8.Internal.Configuration;
using _1Dev.Pagin8.Internal.Tokenizer.Contracts;
using _1Dev.Pagin8.Internal.Tokenizer.Operators;

namespace _1Dev.Pagin8.Internal.Tokenizer.Tokens;

public class IsToken : FilterToken, INegationAware
{
    public IsToken(string field, string value, bool isNegated, bool isEmptyQuery, int nestingLevel, string? comment)
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
        sb.Append(ConfigurationProvider.Config.IsOperator);
        sb.Append('.');

        if (IsNegated)
        {
            var negation = ConfigurationProvider.Config.Negation;
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