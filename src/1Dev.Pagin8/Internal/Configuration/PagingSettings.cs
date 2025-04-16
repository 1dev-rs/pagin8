namespace _1Dev.Pagin8.Internal.Configuration;
public record PagingSettings
{
    public int MaxItemsPerPage { get; set; }

    public int DefaultPerPage { get; set; }

    public int MaxSafeItemCount { get; set; }
}
