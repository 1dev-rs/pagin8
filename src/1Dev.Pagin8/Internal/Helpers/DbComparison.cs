namespace _1Dev.Pagin8.Internal.Helpers;

internal record DbComparison(string Column, dynamic Value)
{
    public string Column { get; set; } = Column;

    public dynamic Value { get; set; } = Value;
}