using _1Dev.Pagin8.Internal.Validators.Contracts;

namespace _1Dev.Pagin8.Extensions.Backend.Extensions;

/// <summary>
/// Options for <see cref="DuckDbServiceCollectionExtensions.AddPagin8BackendDuckDb(Microsoft.Extensions.DependencyInjection.IServiceCollection, Func{IServiceProvider, DuckDB.NET.Data.DuckDBConnection}, Action{DuckDbProviderOptions}?)"/>.
/// </summary>
public sealed class DuckDbProviderOptions
{
    /// <summary>
    /// Default command timeout in seconds. Null means no timeout. DuckDB has no statement timeout of
    /// its own, so the query is interrupted when it runs out and a <see cref="TimeoutException"/> is thrown.
    /// </summary>
    public int? CommandTimeout { get; set; }

    /// <summary>
    /// Validates the tokens of DuckDB queries in place of the <see cref="IContextValidator"/> that
    /// <c>AddPagin8</c> registered, e.g. a search policy without the limits only a slower primary
    /// database needs. Null keeps the shared validator.
    /// </summary>
    public Func<IServiceProvider, IContextValidator>? ContextValidator { get; set; }

    /// <summary>
    /// Sorts and pages a [UseTranslit] text property by its translit column (the one its filters
    /// use), the cursor value transliterated as a filter value is. DuckDB sorts text binary, so by the
    /// plain column "Zemun" comes before "beograd" and "Čačak" after both; by the translit column the
    /// order ignores case and diacritics. Off by default: the sort uses the plain column, as before.
    /// </summary>
    public bool SortByTranslit { get; set; }
}
