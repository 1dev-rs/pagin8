# Changelog

All notable changes to Pagin8 (`1Dev.Pagin8` and `1Dev.Pagin8.Extensions.Backend`) are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html). Releases are published from `v*.*.*` git tags.

## [Unreleased]

## [1.7.1-alpha] - 2026-11-26

### Added

- DuckDB: `DuckDbProviderOptions.SortByTranslit` (off by default). When on, a sort by a `[UseTranslit]` text property orders and pages by its `<Property>_translit` column, the one its filters use, and the cursor value is transliterated as a filter value is (`ToLowerBoldLatin`), so a cursor in Cyrillic or Latin pages the same. DuckDB sorts text binary (uppercase before lowercase, diacritics after `z`); by the translit column case and diacritics do not split the order. A sort without a `[UseTranslit]` property, and every sort when the option is off, is emitted as before. The DuckDB visitor builds this sort itself (`DuckDbTokenVisitor`, new constructor taking `sortByTranslit`); the PostgreSQL and SQL Server visitors are not changed.

### Tests

- `IntegrationTests/DuckDbTranslitSortTests`: the order by the translit column both ways, paging to the end with a Latin and a Cyrillic cursor taken from the shown value, the plain column when the option is off, and a sort without a translit property.

## [1.7.0-alpha] - 2026-11-26

### Added

- Cancellation on every provider: `IFilterProvider` (and so `ISqlServerFilterProvider`, `IDuckDbFilterProvider`) has `GetAsync`, `GetCountAsync` and `GetAggregatesAsync` overloads taking a `CancellationToken`. Cancelling it stops the query on the database (PostgreSQL and SQL Server cancel the statement on the server, DuckDB interrupts it) and throws `OperationCanceledException` for that token on every provider; a command timeout still throws the provider's own timeout exception. The overloads are default interface methods, so an `IFilterProvider` implemented elsewhere keeps compiling and ignores the token. The existing overloads are unchanged.
- `AddPagin8BackendDuckDb(Func<IServiceProvider, DuckDBConnection>, Action<DuckDbProviderOptions>?)` and `AddPagin8BackendDuckDb(Func<IServiceProvider, IDuckDbConnectionFactory>, Action<DuckDbProviderOptions>?)`: connections from a service of the application, and `DuckDbProviderOptions.ContextValidator`, which validates DuckDB queries in place of the `IContextValidator` shared by every provider (e.g. a search policy without the limits a slower primary database needs). The existing overloads are unchanged.

### Changed

- DuckDB: text filters run as DuckDB's string functions instead of `ILIKE`, selecting the same rows: `cs` / `stw` / `enw` as `contains` / `starts_with` / `ends_with` over `lower(col)` with the lower-cased value taken literally, and over the plain column when the value has no letters (only ASCII digits and signs, e.g. account numbers and references), in which case `eq` is `col = v` too. On a 1.5-billion-row Parquet store a substring search ran in 23 s instead of 51-66 s, an equality on a reference in 6.9 s instead of 13.6 s. `not.` and `NULL` rows are unchanged; an `eq` value with `%` or `_` keeps `ILIKE` unless `UseIndexFriendlyTextComparisons` is on. PostgreSQL and SQL Server are unchanged.

### Fixed

- DuckDB: tokens inside `and=(...)` / `or=(...)` were emitted by the PostgreSQL visitor, so a text `in` inside a group failed with "Unsupported comparison ~~* for ANY/ALL". Nested tokens now go through the DuckDB visitor.

### Tests

- `IntegrationTests/DuckDbTextMatchTests`: text filters on DuckDB select the rows `ILIKE` selected (case, literal `%` and `_`, `not.` with `NULL` rows, `in` inside a group), under a decimal-comma culture, and emit the string functions.
- `IntegrationTests/NullKeyset/QueryCancellationTests`: cancelling a running page or count on PostgreSQL, SQL Server and DuckDB stops it within a second and throws `OperationCanceledException` for the token; a cancelled token runs nothing; an uncancelled token changes nothing.

## [1.6.6-alpha] - 2026-11-26

### Fixed

- Cursor paging over a nullable column repeated or skipped rows when a page ended on a `NULL`. The strict part of the cursor condition for a `NULL` last value was `col IS NULL`, which brought back every `NULL` row and none of the values after them; it now treats `NULL` as the stand-in the `ORDER BY` uses (`COALESCE(col, min)` / `ISNULL(col, min)`), as pagin8 did before v1.3.20 (review item #12 in `GITHUB_ISSUES.md`). The order of results does not change. PostgreSQL, SQL Server and DuckDB. This applies to an explicit `$null` only: an empty last value (`$empty` or none) on a non-text column keeps the 1.6.4 condition, so a client that sends it for a `0` or `false` still stops early instead of paging through the same rows again.
- `$null` as a text cursor value was read as an empty string, so a page ending on a `NULL` text value started over from the first non-empty value. It is now `NULL`; `$empty` is still the empty string.
- SQL Server: a sort naming the key next to `$key` (`sort(name.asc.x,id.asc.5,$key.5)`) failed with "A column has been specified more than once in the order by list". The repeated column is left out of `ORDER BY`.
- DuckDB: under a culture with a decimal comma, a cursor on a nullable `decimal` column failed with "Could not convert string '-2,50' to INT32"; parameters are now bound under the invariant culture.

### Documentation

- README: send `$null` / `$empty` for a missing last value (a field without one is dropped from the sort of that page); how nullable columns are ordered and why an index cannot serve that order; LINQ mode does not apply `paging=(...)` (unchanged, now also noted in `LinqTokenVisitor`).

### Tests

- `IntegrationTests/NullKeyset`: every nullable type, both directions, page sizes 1, 3 and 7, with and without filters, and sorts over two nullable columns, scrolled to the end on PostgreSQL, SQL Server and DuckDB. Each scroll must return as many rows as `COUNT(*)` of its filter, none twice, in the order of the whole result.
- The PostgreSQL and SQL Server integration tests run on a server of your own when `PAGIN8_TEST_POSTGRES_CONNECTION` / `PAGIN8_TEST_SQLSERVER_CONNECTION` hold an admin connection string, and in Testcontainers otherwise.

### Withdrawn

- 1.6.5-alpha was reverted and should not be used: besides these fixes it changed the order of `NULL` in sorted results and applied paging in LINQ mode, which cut in-memory filtering to `DefaultPerPage` rows.

## [1.6.4-alpha] - 2026-11-25

### Added

- DuckDB provider, next to PostgreSQL and SQL Server: `AddPagin8BackendDuckDb(...)` registers `IDuckDbFilterProvider` (and `IDuckDbSqlQueryBuilder`), which runs the same filter syntax against DuckDB, e.g. over Parquet files through a `read_parquet(...)` view. It needs `DatabaseType.PostgreSql`, because DuckDB runs the PostgreSQL SQL pagin8 emits; the only differences are handled by the provider: text `in` / `not.in` become `OR`-ed / `AND`-ed comparisons (DuckDB refuses `ILIKE ANY(ARRAY[...])`), and `@p0` parameter markers are rewritten to `$p0` when the command runs. A command timeout interrupts the query and throws `TimeoutException`, since DuckDB has no statement timeout. The Backend package references the managed `DuckDB.NET.Data` only; the application adds DuckDB's native library, e.g. `DuckDB.NET.Data.Full`. Nothing changes for PostgreSQL or SQL Server.

## [1.6.1-alpha] - 2026-10-29

### Fixed

- LINQ mode: `is.true` / `is.false` on `bool?` properties threw `InvalidOperationException` ("The binary operator Equal is not defined for the types Nullable<Boolean> and Boolean"). Applies to top-level and nested filters. `null` values follow SQL semantics: they do not match `is.true` / `is.false`, and they match `not.is.true` / `not.is.false`.

## [1.6.0-alpha] - 2026-10-22

### Upgrade notes

This release contains behavior changes. Review them before upgrading:

- **Invalid query input now always throws `Pagin8Exception`.** Before, some invalid input threw `ArgumentException`, `NotSupportedException`, `NotImplementedException`, or a `TargetInvocationException` that wrapped the real error. If you catch any of those to return `400 Bad Request`, catch `Pagin8Exception` instead. See [Error Handling](README.md#-error-handling).
- **Inner fields of nested filters are validated.** `field.with=(...)` now rejects inner fields that do not exist on the nested type, and inner properties marked `[NonFilterable]`, with `Pagin8_TokenFieldInvalid`. Before, unknown inner fields crashed with a `NullReferenceException`, and `[NonFilterable]` was ignored.
- **`sort(...)` and `limit.N` are rejected inside groups and nested filters.** Before, they were emitted into the `WHERE` clause and produced invalid SQL. They now throw `Pagin8_NestedLevelOperation`.
- **LINQ mode uses the same date formats as SQL mode.** Values such as `15.01.2024`, or values with a time zone suffix, are now rejected with `Pagin8_InvalidComparison` instead of being parsed with the server culture.
- **JSON date fields are compared as dates.** Nested date fields are cast with `::timestamp` on PostgreSQL and `TRY_CONVERT(DATETIME2, ...)` on SQL Server. On PostgreSQL, a JSON date value that is not a valid timestamp (including an empty string) now makes the query fail.
- **`[Aggregate]` with `Sum`, `Avg`, `Min` or `Max` on a non-numeric property now throws `InvalidOperationException`.** Before, it generated invalid SQL.
- **Column metadata for collections of simple values changed.** `List<DateTime>`, `List<DateOnly>` and similar types no longer list the members of the struct (`now`, `ticks`, ...) under `properties`. `List<T?>` reports the element type instead of `object`.

### Added

- `DateOnly` and `DateOnly?` support in all operators, sorting, cursor paging, nested filters and `incl`/`excl`, in PostgreSQL, SQL Server and LINQ modes. `ago`/`for` use whole-day bounds for `DateOnly` properties.
- `date-only` flag in column metadata. `type` stays `DateTime` for backward compatibility.
- Dapper type handler for `DateOnly`, registered automatically by the Backend `FilterProvider` unless the application registered its own.
- `incl`/`excl` on PostgreSQL supports `bigint`, `smallint`, `float`, `boolean`, `uuid`, `timestamp` and `date` arrays.
- `Pagin8_InvalidArrayValue` status code.
- Sorting nullable `float` columns.

### Fixed

- **Security:** `incl`/`excl` values were concatenated into the SQL text. A value containing a quote broke the query, and crafted input could change the SQL. Values are now converted to the element type and sent as parameters. JSON literals for complex arrays are JSON-encoded.
- The metadata caches stored every unknown field name from the query string, so memory use could grow without bound.
- SQL Server parsed `double` values with the server culture (e.g. `1.5` became `15` under `sr-Latn`).
- `ago`/`for` on a field inside a JSON array threw `NullReferenceException`.
- `in` on a JSON date field never matched, and `ago`/`for` on a JSON date field produced invalid SQL on PostgreSQL.
- SQL Server: sorting or cursor paging on a nullable date column used `DateTime.MinValue` as the `NULL` fallback, which SQL `datetime` parameters cannot hold. It now uses `1753-01-01`.
- LINQ mode: `not.ago`/`not.for` and `is.$empty` threw on non-nullable value-type properties.
- LINQ mode: `DateTime?` comparisons now use the date part, like SQL mode does.
- LINQ mode: `incl`/`excl` on `List<T?>` threw `InvalidCastException`.
- LINQ mode: the sort expression was bound to the wrong parameter.

### Known limitations

- LINQ mode (`IQueryableTokenProcessor<T>`) applies filters only. `paging` (sort, limit, cursor, count) and `select` are accepted but ignored. See [In-Memory Filtering](README.md#-in-memory-filtering-linq).
- Date values with a time zone suffix (`Z`, `+02:00`) are not accepted.

## Earlier releases

Changes before this changelog was introduced are available in the [git tag history](https://github.com/1dev-rs/pagin8/tags).
