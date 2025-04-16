using _1Dev.Pagin8.Internal.Tokenizer.Contracts;
using _1Dev.Pagin8.Internal.Tokenizer.Operators;

namespace _1Dev.Pagin8.Internal.Tokenizer.Tokens;

public class ArrayOperationToken(string field, List<string> values, ArrayOperationType operationType) : FilterToken
{
    public string Field { get; private set; } = field;
    public List<string> Values { get; private set; } = values;
    public ArrayOperationType OperationType { get; private set; } = operationType;

    public override QueryBuilderResult Accept<T>(ISqlTokenVisitor visitor, QueryBuilderResult result) => visitor.Visit<T>(this, result);

    public override IQueryable<T> Accept<T>(ILinqTokenVisitor<T> visitor, IQueryable<T> source) => visitor.Visit(this, source);

    public override string RevertToQueryString()
    {
        var valuesFormatted = string.Join(",", Values);
        switch (OperationType)
        {
            case ArrayOperationType.Include:
                return $"{Field}.incl({valuesFormatted})";
            case ArrayOperationType.Exclude:
                return $"{Field}.excl({valuesFormatted})";
            default:
                throw new InvalidOperationException("Unsupported operation type.");
        }
    }
}