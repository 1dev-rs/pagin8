using System.Text;
using _1Dev.Pagin8.Internal.Tokenizer.Contracts;
using _1Dev.Pagin8.Internal.Tokenizer.Operators;
using Pagin8.Internal.Configuration;

namespace _1Dev.Pagin8.Internal.Tokenizer.Tokens;

public class ComparisonToken : FilterToken, INegationAware
{
    public ComparisonToken(string field, ComparisonOperator @operator, string value, bool isNegated, int nestingLevel, string? comment, string jsonPath = "")
    {
        Field = field;
        Operator = @operator;
        Value = value;
        IsNegated = isNegated;
        NestingLevel = nestingLevel;
        JsonPath = jsonPath;
        Comment = comment;
    }

    public override QueryBuilderResult Accept<T>(ISqlTokenVisitor visitor, QueryBuilderResult result) => visitor.Visit<T>(this, result) ;

    public override IQueryable<T> Accept<T>(ILinqTokenVisitor<T> visitor, IQueryable<T> source) => visitor.Visit(this, source);

    public override string RevertToQueryString()
    {
        var sb = new StringBuilder(Field);

        sb.Append(NestingLevel == 1 ? '=' : '.');

        if (IsNegated)
        {
            var negation = EngineDefaults.Config.Negation;
            sb.Append($"{negation}.");
        }

        sb.Append(Operator.GetQueryFromComparison());
        sb.Append('.');
        sb.Append(Value);

        if (!string.IsNullOrWhiteSpace(Comment))
        {
            sb.Append('^').Append(Comment);
        }

        return sb.ToString();
    }

    public string Field { get; } 

    public string Value { get; }

    public ComparisonOperator Operator { get; }

    public bool IsNegated { get; }

    public int NestingLevel { get; }
}