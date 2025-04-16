using _1Dev.Pagin8.Internal.Attributes.Helpers;

namespace _1Dev.Pagin8.Internal.Attributes;

[AttributeUsage(AttributeTargets.Property)]
public class TranslitAttribute(IndexType indexType = IndexType.None) : Attribute
{
    public IndexType IndexType { get; set; } = indexType;
}