# Changelog

All notable changes to Pagin8 (`1Dev.Pagin8` and `1Dev.Pagin8.Extensions.Backend`) are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html). Releases are published from `v*.*.*` git tags.

## [Unreleased]

## [1.6.5-alpha] - 2026-11-26

### Upgrade notes

- **`NULL` in a sorted nullable column is ordered by the column itself.** PostgreSQL and DuckDB emit `col ASC NULLS FIRST` / `col DESC NULLS LAST`, SQL Server `col ASC` / `col DESC`, instead of `COALESCE(col, <min>)` / `ISNULL(col, <min>)`. `NULL` stays the smallest value, as before; what changes is that it now sorts strictly before `''`, `-1` and `DateTime.MinValue`, which it used to tie with (the tie was broken by the key). An index on the column (`(col NULLS FIRST, key)` on PostgreSQL) now serves the order; an expression index on `COALESCE(col, ...)` no longer does.
- **`$null` is a `NULL` cursor value for text too.** It used to become `''`, so a text cursor on a `NULL` row compared as an empty string.

### Fixed

- Cursor paging over a nullable column repeated or skipped rows when a page ended on a `NULL`: the condition for the strict part of the cursor was `col IS NULL`, which brought back every `NULL` row and none of the values after them. Rows after a `NULL` cursor are now every non-`NULL` row ascending and none descending, and rows after a value descending include the `NULL` rows. Regression from the change for review item #12 (first released in v1.3.20-alpha) (`GITHUB_ISSUES.md`), which replaced the consistent `COALESCE(col, min) > COALESCE(value, min)` with `IS NULL` for a `NULL` cursor. Applies to PostgreSQL, SQL Server, DuckDB and LINQ.
- Text columns: the `COALESCE` stand-in for `NULL` was sent as the two-character string `''`, not as an empty string.
- SQL Server: a sort naming the key and `$key` (`sort(name.asc,id.asc,$key.45)`) failed with "A column has been specified more than once in the order by list". A column already in the order is no longer repeated.
- LINQ mode: `paging=(...)` was ignored: the sort, cursor and limit were built and then dropped. Cursor paging on `bool` columns threw `Pagin8_UnsupportedComparison`.
- Sorting a nullable column of a type without a stand-in value (e.g. `Guid?`) threw `Pagin8_ColumnNotSortable`.

### Tests

- `IntegrationTests/NullKeyset`: every sortable nullable type, both directions, page sizes 1, 3 and 7, with and without filters, and sorts over two nullable columns, scrolled to the end on PostgreSQL, SQL Server, DuckDB and LINQ. Each scroll must return as many rows as `COUNT(*)` of its filter, none twice, in order. On PostgreSQL, `EXPLAIN` checks that an index on `(col NULLS FIRST, id)` serves the order without a sort.
- The PostgreSQL and SQL Server integration tests run on a server of your own when `PAGIN8_TEST_POSTGRES_CONNECTION` / `PAGIN8_TEST_SQLSERVER_CONNECTION` hold an admin connection string (a fresh database is created and dropped), and in Testcontainers otherwise.

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
