namespace _1Dev.Pagin8.Internal;

public record AdditionalInfoMeta(bool ShowFilters, bool ShowColumns, bool ShowSubscriptions)
{ public static AdditionalInfoMeta Create(bool showFilters = false, bool showColumns = false, bool showSubscriptions = false)
    {
        return new AdditionalInfoMeta(showFilters, showColumns, showSubscriptions);
    }

    public static AdditionalInfoMeta CreateDefault()
    {
        return new AdditionalInfoMeta(false, false, false);
    }

    public bool ShowFilters { get; set; } = ShowFilters;
    public bool ShowColumns { get; set; } = ShowColumns;
    public bool ShowSubscriptions { get; set; } = ShowSubscriptions;
}


