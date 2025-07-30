using System.Text;
using _1Dev.Pagin8.Internal.Tokenizer.Contracts;
using _1Dev.Pagin8.Internal.Tokenizer.Operators;

namespace _1Dev.Pagin8.Internal.Tokenizer.Tokens;

public class DateRangeToken : FilterToken, INegationAware
{
    public DateRangeToken(string field,
        DateRangeOperator @operator,
        int value,
        DateRange range,
        bool exact,
        bool strict,
        int nestingLevel,
        bool isNegated, string? comment = null)
    {
        Field = field;
        Operator = @operator;
        Value = value;
        Range = range;
        Exact = exact;
        Strict = strict;
        NestingLevel = nestingLevel;
        IsNegated = isNegated;
        Comment = comment;
    }

    public override QueryBuilderResult Accept<T>(ISqlTokenVisitor visitor, QueryBuilderResult result) => visitor.Visit<T>(this, result);

    public override string RevertToQueryString()
    {
        var sb = new StringBuilder(Field);

        sb.Append(NestingLevel == 1 ? '=' : '.');

        sb.Append(IsNegated ? "not." : "");

        sb.Append(Operator.GetQueryFromDateRange());

        sb.Append($".{Value}");

        sb.Append(Range.GetCharFromDateRange());

        sb.Append($"{(Exact ? "e" : "")}");

        sb.Append($"{(Strict ? "s" : "")}");

        if (!string.IsNullOrWhiteSpace(Comment))
        {
            sb.Append('^').Append(Comment);
        }

        return sb.ToString();
    }

    public override IQueryable<T> Accept<T>(ILinqTokenVisitor<T> visitor, IQueryable<T> source) => visitor.Visit(this, source);

    public string Field { get; }

    public DateRangeOperator Operator { get; }

    public int Value { get; }

    public DateRange Range { get; }

    public bool Exact { get; }

    public bool Strict { get; }

    public int NestingLevel { get; }

    public bool IsNegated { get; }
}