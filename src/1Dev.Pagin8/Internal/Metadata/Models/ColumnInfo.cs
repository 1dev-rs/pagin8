namespace _1Dev.Pagin8.Internal.Metadata.Models;

public record ColumnInfo(string Name, Type Type, bool IsTranslit = false, bool IsNullAllowed = false, bool IsKey = false)
{
    public string Name { get; set; } = Name;

    public bool IsTranslit { get; set; } = IsTranslit;

    public bool IsNullAllowed { get; set; } = IsNullAllowed;

    public Type Type { get; set; } = Type;

    public bool IsKey { get; set; } = IsKey;
}