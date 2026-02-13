namespace _1Dev.Pagin8.Internal.Configuration;
public record ServiceConfiguration
{
    public DatabaseType DatabaseType { get; set; }

    public int MaxNestingLevel { get; set; }

    public PagingSettings PagingSettings { get; set; } = new();

    public int MaxRelativeDateYears { get; set; } = 20;

    public bool IgnoreUnknownSelectFields { get; set; }
}
