using _1Dev.Pagin8.Internal.Tokenizer.Contracts;
using _1Dev.Pagin8.Internal.Tokenizer.Operators;
using Pagin8.Internal.Configuration;
using System.Text;

namespace _1Dev.Pagin8.Internal.Tokenizer.Tokens;

public class ArrayOperationToken : FilterToken
{
    public ArrayOperationToken(string field, List<string> values, ArrayOperator operationType, bool isNegated, int nestingLevel, string? comment = null)
    {
        Field = field;
        Values = values;
        Operator = operationType;
        IsNegated = isNegated;
        NestingLevel = nestingLevel;
        Comment = comment;
    }

    public string Field { get; private set; }

    public List<string> Values { get; private set; }

    public ArrayOperator Operator { get; private set; }

    public bool IsNegated { get; private set; }

    public int NestingLevel { get; private set; }

    public override QueryBuilderResult Accept<T>(ISqlTokenVisitor visitor, QueryBuilderResult result) => visitor.Visit<T>(this, result);

    public override IQueryable<T> Accept<T>(ILinqTokenVisitor<T> visitor, IQueryable<T> source) => visitor.Visit(this, source);

    public override string RevertToQueryString()
    {
        var sb = new StringBuilder();

        sb.Append(Field);

        sb.Append(NestingLevel == 1 ? '=' : '.');

        if (IsNegated)
        {
            var negation = EngineDefaults.Config.Negation;
            sb.Append($"{negation}.");
        }

        sb.Append(Operator.GetDslOperator());

        var valuesFormatted = string.Join(",", Values);
        sb.Append('(').Append(valuesFormatted).Append(')');

        if (!string.IsNullOrWhiteSpace(Comment))
        {
            sb.Append('^').Append(Comment);
        }

        return sb.ToString();
    }

}