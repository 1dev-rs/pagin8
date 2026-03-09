## What changed and why

This branch does two things at once:

1. **Adds a full SQL Server provider** alongside the existing PostgreSQL one, so consumers can point Pagin8 at either database engine with a single DI call.
2. **Closes GitHub issues #5, #6, #7, #8, #9, #10, #11, #12–#17** — a batch of validation gaps, a SQL-injection vector, a reflection-cache miss, and two filtering correctness bugs that had been open since the `feature/add-sql-server-provider-for-Pagin8` work started.

---

## 1. New SQL Server Provider

### Core library (`src/1Dev.Pagin8/`)

| File | What it does |
|---|---|
| `Internal/Visitors/SqlServerTokenVisitor.cs` *(new, ~1 000 lines)* | Translates every Pagin8 DSL token into T-SQL syntax — mirrors `NpgsqlTokenVisitor` but handles SQL Server specifics (JSON via `JSON_VALUE`, string functions, parameter naming). |
| `Internal/SqlServerSqlQueryBuilder.cs` *(new)* | Thin builder that wires `SqlServerTokenVisitor` to the public query-building pipeline. |
| `ISqlServerSqlQueryBuilder.cs` *(new)* | Public interface for the builder, placed next to the existing `ISqlQueryBuilder`. |
| `Internal/Configuration/DatabaseType.cs` | `SqlServer` value added to the enum. |
| `Extensions/ServiceCollectionExtensions.cs` | `AddPagin8SqlServer(...)` extension registered. |

### Extensions.Backend project (`src/1Dev.Pagin8.Extensions.Backend/`)

| File | What it does |
|---|---|
| `Implementations/SqlServerConnectionFactory.cs` *(new)* | Manages a named SQL Server `DbConnection`; guards against null/empty inputs. |
| `Implementations/SqlServerDbConnectionFactoryProvider.cs` *(new)* | Resolves the correct `SqlServerConnectionFactory` by provider name for multi-tenant scenarios. |
| `Implementations/SqlServerFilterProvider.cs` *(new)* | `IFilterProvider` implementation that uses `SqlServerTokenVisitor` under the hood. |
| `Interfaces/ISqlServerDbConnectionFactory.cs` *(new)* | Contract for the per-database connection factory. |
| `Interfaces/ISqlServerDbConnectionFactoryProvider.cs` *(new)* | Contract for the provider resolver. |
| `Interfaces/ISqlServerFilterProvider.cs` / `ISqlServerFilterProviderFactory.cs` *(new)* | Public filter-provider interfaces. |
| `Extensions/ServiceCollectionExtensions.cs` | Adds `AddPagin8BackendSqlServer(...)` — registers all SQL Server services into DI; updated `AddPagin8Backend(...)` to keep Postgres path unchanged. |
| `Implementations/FilterProvider.cs` | Updated with provider-selection logic so callers can request SQL Server or Postgres by name. |
| `Interfaces/IFilterProvider.cs` | Signature extended to accept an optional provider-name parameter. |
| `Models/FilteredDataQuery.cs` | `IsJson` flag + `Create(...)` overload added — signals that the target column is a JSON column so the visitor picks the correct operator path. |

---

## 2. Bug Fixes (GitHub Issues)

### #8 — SQL injection via JSON field names
**File:** `Internal/Tokenizer/Operators/SqlOperatorConstants.cs`  
JSON field names were interpolated directly into the generated SQL string. The constant now validates and sanitizes field names against an allowlist before embedding them.

### #9 — Reflection cache miss (performance)
**File:** `Internal/Helpers/TokenHelper.cs`  
Property lookups via reflection were repeated on every filter evaluation. A static `ConcurrentDictionary` cache was added; lookup is now O(1) after the first call per type.

### #10 — LINQ nested filter support
**File:** `Internal/Visitors/LinqTokenVisitor.cs`  
Multi-level `AND`/`OR` group nesting was collapsed into a flat predicate. The visitor now recursively builds `Expression.AndAlso` / `Expression.OrElse` trees for arbitrarily deep groups.

### #11 — JSON path `IN` operator
**Files:** `Internal/Tokenizer/Strategy/InTokenizationStrategy.cs`, both visitors  
The `in` operator failed when the left-hand side was a JSON path expression. The tokenization strategy now detects JSON paths and emits the correct `jsonb @> any(...)` (Postgres) / `JSON_VALUE(...) IN (...)` (SQL Server) form.

### #5, #6, #7, #12–#17 — Validation & guard hardening
**Files:** `Internal/Utils/Guard.cs`, `Internal/Exceptions/Base/Pagin8Exception.cs`, `Internal/Tokenizer/Strategy/IsTokenizationStategy.cs`, `Internal/Tokenizer/TokenizationService.cs`, `Internal/Tokenizer/Tokenizer.cs`, `Internal/DateProcessor/DateProcessor.cs`  
Public entry points now call `Guard.NotNull` / `Guard.NotEmpty` before processing. `Pagin8Exception` carries a structured message including the offending token for easier debugging.

---

## 3. Postgres Visitor Improvements
**File:** `Internal/Visitors/NpgsqlTokenVisitor.cs`  
Brought in sync with SQL Server visitor: same nested-group logic (#10), same JSON-path `IN` handling (#11), and same sanitized JSON field embedding (#8).

---

## 4. Central Package Management
**Files:** `src/Directory.Build.props` *(new)*, `src/Directory.Packages.props` *(new)*  
All `<PackageReference>` `Version` attributes removed from individual `.csproj` files and moved here. Ensures every project in the solution uses identical package versions.

---

## 5. Tests

### New unit tests
| File | Covers |
|---|---|
| `FilterProviderPostgreGenerationTests.cs` | SQL generation assertions for the Postgres filter provider. |
| `FilterProviderSqlServerGenerationTests.cs` | Same for SQL Server. |
| `SqlQueryBuilderTests/ParameterizationTests.cs` | Confirms every user-supplied value is parameterized — not interpolated — in both backends. |
| `SqlQueryBuilderTests/SqlServerQueryBuilderTests.cs` | Token-level SQL Server query builder assertions (~378 lines). |
| `SqlQueryBuilderTests/PostgreSqlQueryBuilderTests.cs` | Postgres equivalent (renamed from old `SqlQueryBuilderTests.cs`). |
| `LinqNestedFilterAndJsonPathInTests.cs` | LINQ visitor for nested predicates and JSON path `IN`. |

### New Testcontainers integration tests
| File | Covers |
|---|---|
| `IntegrationTests/Fixtures/PostgreSqlContainerFixture.cs` | Spins up a real Postgres container per collection. |
| `IntegrationTests/Fixtures/SqlServerContainerFixture.cs` | Same for SQL Server. |
| `IntegrationTests/PostgreSqlContainerIntegrationTests.cs` *(392 lines)* | Paging, sorting, filtering, JSON queries, nested groups, IN lists, performance thresholds — against a live Postgres DB. |
| `IntegrationTests/SqlServerContainerIntegrationTests.cs` *(389 lines)* | Same scenarios against a live SQL Server DB. |
| `IntegrationTests/Data/TestDataSeeder.cs` | Seeds deterministic `Product` rows used by all integration tests. |
| `IntegrationTests/Configuration/TestConfiguration.cs` | Reads `test-config.json` for container image/port/credential overrides. |
| `IntegrationTests/run-testcontainers.ps1` / `.bat` | CI/local helper scripts to spin containers and run tests. |

### Infrastructure
- `xunit.runner.json` — parallel execution configured consistently.
- `SqlQueryBuilderTests/Internal/SqlServerTestBootstrap.cs` *(new)* — bootstrap for SQL Server unit test collection.
- `SqlQueryBuilderTests/Internal/PostgreSqlTestBootstrap.cs` — renamed from old `TestBootstrap.cs`.

---

## Files NOT changed
Everything under `src/1Dev.Pagin8/Input/`, `src/1Dev.Pagin8/IMetadataProvider.cs`, `src/1Dev.Pagin8/IPagin8MetadataProvider.cs`, `src/1Dev.Pagin8/IQueryableTokenProcessor.cs` — public API surface for the existing Postgres-only path is untouched.
