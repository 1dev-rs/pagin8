namespace _1Dev.Pagin8.Internal;

public record AdditionalInfoMeta(bool ShowColumns)
{
    public static AdditionalInfoMeta Create(bool showColumns) => new(showColumns);

    public static AdditionalInfoMeta CreateDefault() => new(false);
}
