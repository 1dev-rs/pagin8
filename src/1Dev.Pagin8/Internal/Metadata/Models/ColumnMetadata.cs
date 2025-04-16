using _1Dev.Pagin8.Internal.Attributes;

namespace _1Dev.Pagin8.Internal.Metadata.Models;

[NonLocalizable]
public record ColumnMetadata(string Name, string Type, List<string> Flags)
{
    public string Name { get; set; } = Name;

    public string Type { get; set; } = Type;

    public List<string> Flags { get; set; } = Flags;

    public List<ColumnMetadata> Properties { get; set; } = [];

    public bool ShouldSerializeFlags() => Flags is { Count: > 0 };

    public bool ShouldSerializeProperties() => Properties is { Count: > 0 };
}