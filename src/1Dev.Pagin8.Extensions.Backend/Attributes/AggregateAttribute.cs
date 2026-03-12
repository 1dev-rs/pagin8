
namespace Attributes;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
public sealed class AggregateAttribute(AggregateType aggregateType = AggregateType.Sum) : Attribute
{
    public AggregateType AggregateType { get; } = aggregateType;
}

public enum AggregateType
{
    Sum,
    Count,
    Min,
    Max,
    Avg
}
