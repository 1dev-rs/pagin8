namespace _1Dev.Pagin8.Internal.Tokenizer.Tokens.Sort;

public class SortExpression(string field, SortOrder sortOrder, string lastValue = "")
{
    public string Field { get; set; } = field;

    public SortOrder SortOrder { get; } = sortOrder;

    public string LastValue { get; } = lastValue;
}