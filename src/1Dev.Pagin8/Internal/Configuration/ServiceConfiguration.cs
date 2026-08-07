namespace _1Dev.Pagin8.Internal.Configuration;
public record ServiceConfiguration
{
    public DatabaseType DatabaseType { get; set; }

    public int MaxNestingLevel { get; set; }

    public PagingSettings PagingSettings { get; set; } = new();

    public int MaxRelativeDateYears { get; set; } = 20;

    public bool IgnoreUnknownSelectFields { get; set; }

    // Postgres only. Off by default — do not flip this for an existing consumer without checking
    // its indexes first (see AddPagin8 XML doc). When on: `eq`/`in`/`stw` on text columns emit
    // `lower(column) = lower(@value)` / `lower(column) LIKE lower(@value)||'%'` instead of
    // `column ILIKE @value` — same result set (values are already lower-cased before this flag
    // existed), but sargable against a plain `lower(column)` btree index, which ILIKE never was.
    // `cs`/`enw` (contains/ends-with) are unaffected either way — no WHERE-clause rewrite makes a
    // substring/suffix scan index-friendly on a plain btree; that needs pg_trgm regardless.
    public bool UseIndexFriendlyTextComparisons { get; set; }
}
