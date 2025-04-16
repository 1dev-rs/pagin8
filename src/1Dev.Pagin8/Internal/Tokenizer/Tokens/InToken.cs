using System.Text;
using _1Dev.Pagin8.Internal.Configuration;
using _1Dev.Pagin8.Internal.Tokenizer.Contracts;
using _1Dev.Pagin8.Internal.Tokenizer.Operators;

namespace _1Dev.Pagin8.Internal.Tokenizer.Tokens;

public class InToken : FilterToken, INegationAware
{
    public InToken(string field, string values, int nestingLevel, string? comment, bool isNegated = false, ComparisonOperator comparison = ComparisonOperator.Equals)
    {
        Field = field;
        Values = values;
        NestingLevel = nestingLevel;
        IsNegated = isNegated;
        Comparison = comparison;
        Comment = comment;
    }

    public override QueryBuilderResult Accept<T>(ISqlTokenVisitor visitor, QueryBuilderResult result) => visitor.Visit<T>(this, result);

    public override string RevertToQueryString()
    {
        var sb = new StringBuilder();

        sb.Append(Field);

        sb.Append(NestingLevel == 1 ? "=" : ".");

        var negation = ConfigurationProvider.Config.Negation;
        if (IsNegated)
        {
            sb.Append(negation);
            sb.Append('.');
        }

        if (Comparison != ComparisonOperator.Equals)
        {
            sb.Append(Comparison.GetQueryFromComparison());
            sb.Append('.');
        }

        sb.Append("in.");

        sb.Append(Values);

        if (!string.IsNullOrWhiteSpace(Comment))
        {
            sb.Append('^').Append(Comment);
        }

        return sb.ToString();
    }

    public override string ToString()
    {
        return $"{Field} {Values}, Negated: {IsNegated}";
    }

    public override IQueryable<T> Accept<T>(ILinqTokenVisitor<T> visitor, IQueryable<T> source) => visitor.Visit(this, source);

    public string Field { get; }

    public string Values { get; }

    public int NestingLevel { get; }

    public bool IsNegated { get; }

    public ComparisonOperator Comparison { get; }
}