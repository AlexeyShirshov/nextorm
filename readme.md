# Next ORM

[![NuGet](https://img.shields.io/nuget/vpre/nextorm?logo=nuget)](https://www.nuget.org/packages/nextorm)
[![Build](https://github.com/AlexeyShirshov/nextorm/actions/workflows/dotnet.yml/badge.svg)](https://github.com/AlexeyShirshov/nextorm/actions/workflows/dotnet.yml)
[![Docs](https://github.com/AlexeyShirshov/nextorm/actions/workflows/docs.yml/badge.svg)](https://alexeyshirshov.github.io/nextorm/)
[![License: MIT](https://img.shields.io/github/license/AlexeyShirshov/nextorm)](https://github.com/AlexeyShirshov/nextorm/blob/main/LICENSE)

Nextorm is a high-performance, zero-SQL object-relational mapper for .NET. It generates typed SQL
from LINQ-like expressions and maps rows back to classes, interfaces, anonymous types or tuples —
with no change tracking and no mandatory entity class.

> **Documentation: [alexeyshirshov.github.io/nextorm](https://alexeyshirshov.github.io/nextorm/)**

## Features

* **Query compilation and parameterization** — SQL is built once and cached; values are bound as parameters.
* **Two ways to reuse a query** — the implicit plan cache and the explicit
  [`Prepare()`](https://alexeyshirshov.github.io/nextorm/engine/03-plan-cache-and-reuse.html).
* **Entities are optional** — query tables and columns directly with
  [`TableAlias`](https://alexeyshirshov.github.io/nextorm/guide/30-projections.html), or map with attributes / fluent API.
* **Rich projection targets** — classes, interfaces, records, anonymous types, tuples, scalars and nested projections.
* **No change tracking** — lean, allocation-conscious materialization built for read-heavy data access.
* **Six database engines** — SQLite, SQL Server, PostgreSQL, MySQL, MariaDB, ClickHouse, plus a built-in in-memory provider.
* **Native extreme-row selection** — [`SelectWhereMax`/`SelectWhereMin`](https://alexeyshirshov.github.io/nextorm/advanced/select-where-extrema-native.html)
  automatically use PostgreSQL `DISTINCT ON`/`LIMIT 1` and ClickHouse `argMin`/`argMax`, with a
  portable window-function fallback elsewhere.
* **Extensible without forking** — public query components (`IQueryComponent` + `WithComponent`),
  global query filters, value converters, SQL functions, interceptors, custom dialects and renderers.
  No `InternalsVisibleTo`, no translator registry and no global mutable state.
* **Zero-config NativeAOT** — `PublishAot=true` is the only change: no compiled model, no precompilation
  step, no interceptor opt-in and no manual entity/query registration. Partial coverage today (SQLite,
  .NET 11 / `linux-x64`); see the
  [NativeAOT guide](https://alexeyshirshov.github.io/nextorm/infrastructure/05-nativeaot.html).

## Installation

```bash
dotnet add package nextorm
dotnet add package nextorm.sqlite   # pick one provider
```

1.0.9 is the first stable release, so `--prerelease` is not needed; previews (`1.0.10-a.x` and later)
still require it. Package Manager Console equivalent: `Install-Package nextorm`.

| Package | Provider | Driver |
|---|---|---|
| `nextorm` | Core engine + in-memory provider | — |
| `nextorm.sqlite` | SQLite | `Microsoft.Data.Sqlite` |
| `nextorm.sqlserver` | SQL Server | `Microsoft.Data.SqlClient` |
| `nextorm.postgres` | PostgreSQL | `Npgsql` |
| `nextorm.mysql` | MySQL | `MySqlConnector` |
| `nextorm.mariadb` | MariaDB | `MySqlConnector` |
| `nextorm.clickhouse` | ClickHouse | `ClickHouse.Driver` |
| `nextorm.extensions` | Query components: `SelectWhereMin`/`SelectWhereMax`, `SelectGroup`/`SelectGroupHaving` | — |
| `nextorm.entityframeworkcore` | EF Core integration: `db.CreateNextOrmContext()` over an existing `DbContext` | `Microsoft.EntityFrameworkCore.Relational` |

## Quick start

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.Data.Sqlite;
using NextORM.Core;
using NextORM.Sqlite;

var connection = new SqliteConnection("Data Source=:memory:");
connection.Open();

var builder = new DataContextBuilder().UseSqlite(connection);
using var dataContext = builder.CreateDataContext();

await foreach (var row in dataContext.From<ISimpleEntity>()
                                     .Select(entity => new { entity.Id })
                                     .ToAsyncEnumerable())
{
    Console.WriteLine($"Id = {row.Id}");
}

[SqlTable("simple_entity")]
public interface ISimpleEntity
{
    [Key]
    [Column("id")]
    int Id { get; set; }
}
```

The generated SQL is a plain `select id from simple_entity`. Continue with the
[Quickstart guide](https://alexeyshirshov.github.io/nextorm/getting-started/02-quickstart.html).

## Query reuse

Avoid rebuilding a query plan on every execution in two independent ways: the **implicit plan cache**
(used automatically by the terminal methods) and the explicit
[`Prepare`](https://alexeyshirshov.github.io/nextorm/api/NextORM.Core.EntityBuilder-1.html) returning an
[`IPreparedQueryCommand<TResult>`](https://alexeyshirshov.github.io/nextorm/api/NextORM.Core.IPreparedQueryCommand-1.html).
They differ in cost, lifetime and thread-safety rules — see
[Query reuse: cache vs Prepare](https://alexeyshirshov.github.io/nextorm/engine/03-plan-cache-and-reuse.html).

## Extensibility

Extensibility is a first-class part of the design, not a set of internal escape hatches. Every hook
is public and usable from a separate assembly — there is no `InternalsVisibleTo`, no translator
registry and no global mutable state.

- **Query components (structural operators).** Implement
  [`IQueryComponent`](https://alexeyshirshov.github.io/nextorm/advanced/extensibility.html#extending-query-construction-with-query-components)
  and attach it with `WithComponent`. A component validates itself in `Prepare`, contributes SQL
  structure in `BuildQuery` and carries a descriptor that participates in the plan key, so a warm
  plan-cache hit does not rebuild it. The
  [`nextorm.extensions`](https://alexeyshirshov.github.io/nextorm/advanced/extensions-package.html)
  package is built entirely on this surface:

  ```csharp
  using NextORM.Extensions;

  var leaders = dataContext.From<Player>()
                           .SelectWhereMax(p => p.Score, groupBy: p => p.TeamId)
                           .Select(p => new { p.Id, p.TeamId, p.Score });
  ```

  `SelectWhereMax`/`SelectWhereMin` and `SelectGroup`/`SelectGroupHaving` are ordinary components —
  your own package can add operators the same way, with the same plan cache and parameter binding.

- **Configuration hooks** — global query filters (tenant isolation, soft delete), value converters,
  naming conventions, `[SqlFunction]` declarations and query/connection interceptors.
- **Custom dialects and renderers** — subclass a provider context and its dialect (and, on
  PostgreSQL and ClickHouse, the native extreme-row renderer) when the emitted SQL itself must change.

The complete guide — which hook to choose, who calls it and when, and a working example for each —
is [Extensibility](https://alexeyshirshov.github.io/nextorm/advanced/extensibility.html).

## NativeAOT

Partial and experimental: published native binaries run with the SQLite provider on `net11.0`
(`linux-x64`), with JIT-parity checks for interface entities, captured predicates, anonymous
projections, nullable scalars, SQLite JSON functions and grouped aggregates. Every push publishes
the native runner and executes it in CI. The remaining providers, the EF Core bridge and the
residual AOT/trim diagnostics are tracked in
[#370](https://github.com/AlexeyShirshov/nextorm/issues/370); see the
[NativeAOT guide](https://alexeyshirshov.github.io/nextorm/infrastructure/05-nativeaot.html).

That is the lightest AOT migration contract among the compared libraries. EF Core needs a compiled model
plus interceptor-based query precompilation and rejects dynamically composed queries; Dapper needs
`Dapper.AOT` with `[module: DapperAot]`, an `InterceptorsNamespaces` opt-in and statically known SQL;
linq2db does not officially support NativeAOT. On a 13 GB SQLite reporting app nextorm published natively
with no source changes — 8.2 MB binary, 0.12 s, 30 MB RSS — against EF Core's 17.3 MB, 0.23 s and 42 MB
after two code-generation workarounds (Dapper.AOT reaches 3.5 MB and 0.12 s only with hand-written SQL).
The reference app ships in the repository:
[`examples/nextorm.examples.oc-report`](https://github.com/AlexeyShirshov/nextorm/tree/main/examples/nextorm.examples.oc-report).

## Benchmarks

<!-- benchmarks:conditions:start -->
Benchmark conditions: provider SQLite (`Microsoft.Data.Sqlite`); async queries; each cell is the fastest
method for that ORM in the linked report. Machine: AMD Ryzen 7 5800HS with Radeon Graphics (8 logical / 4
physical cores), .NET 11.0.0, BenchmarkDotNet 0.15.8 (`ShortRun` + `InProcessEmitToolchain`). As of
**2026-11-11**. The last twelve rows are the basic-feature scenarios (Category A in the [comparison
article](https://alexeyshirshov.github.io/nextorm/comparisons/benchmarks.html)): Nextorm through `Prepare()`
against the compiled form of EF Core and linq2db and raw Dapper, ten queries per iteration, so each cell there
is the per-query mean. Those rows come from the Category A suite run on **2026-11-11**.
<!-- benchmarks:conditions:end -->

<!-- benchmarks:e2e:start -->
| Method | Nextorm | Dapper | linq2db | EF Core |
|---|:---:|:---:|:---:|:---:|
| [Data fetch](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkIteration-report-github.md) | **10.26 μs** | 14.59 μs | 15.54 μs | 34.36 μs |
| [Wide data fetch](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkLargeIteration-report-github.md) | **7.682 ms** | 8.635 ms | 8.765 ms | 8.981 ms |
| [Where](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkWhere-report-github.md) | **927.9 μs** | 1,353.2 μs | 1,403.5 μs | 3,179.1 μs |
| [Simulate work](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkSimulateWork-report-github.md) | **7.558 ms** | 24.385 ms | 393.247 ms | 102.706 ms |
| [Any](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkAny-report-github.md) | **945.5 μs** | 1,443.5 μs | 1,526.2 μs | 3,401.7 μs |
| [FirstOrDefault](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkFirst-report-github.md) | **96.31 μs** | 140.89 μs | 356.68 μs | 365.25 μs |
| [SingleOrDefault](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkSingle-report-github.md) | **99.09 μs** | 140.28 μs | 361.22 μs | 350.29 μs |
| [`CASE WHEN`](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkFeaturesFair-report-github.md) | **9.64 μs** | 14.07 μs | 15.47 μs | 33.70 μs |
| [CTE](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkFeaturesFair-report-github.md) | **10.13 μs** | 21.60 μs | 22.04 μs | — |
| [`SELECT DISTINCT`](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkFeaturesFair-report-github.md) | **11.54 μs** | 17.84 μs | 18.16 μs | 38.90 μs |
| [`EXCEPT`](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkFeaturesFair-report-github.md) | **11.16 μs** | 17.32 μs | 21.20 μs | 42.61 μs |
| [IN-list](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkFeaturesFair-report-github.md) | **12.90 μs** | 18.15 μs | 43.08 μs | 59.00 μs |
| [`INTERSECT`](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkFeaturesFair-report-github.md) | **10.12 μs** | 16.66 μs | 20.33 μs | 39.48 μs |
| [4-table join](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkFeaturesFair-report-github.md) | **12.98 μs** | 25.84 μs | 24.19 μs | 45.38 μs |
| [`LEFT JOIN`](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkFeaturesFair-report-github.md) | **12.68 μs** | 20.00 μs | 20.96 μs | 40.49 μs |
| [Recursive CTE](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkFeaturesFair-report-github.md) | **12.49 μs** | 24.18 μs | 27.07 μs | — |
| [String functions](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkFeaturesFair-report-github.md) | **9.47 μs** | 14.86 μs | 16.37 μs | 33.21 μs |
| [UDF](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkFeaturesFair-report-github.md) | **9.54 μs** | 14.12 μs | 15.13 μs | 34.16 μs |
| [Window functions](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkFeaturesFair-report-github.md) | **14.45 μs** | 29.70 μs | 31.98 μs | — |
<!-- benchmarks:e2e:end -->

The full comparison — capabilities and benchmarks, with methodology — is in the
[docs](https://alexeyshirshov.github.io/nextorm/comparisons/).

## Status

Stable — 1.0.9 is the first stable release. The public API surface is frozen and tracked from this
version (`PublicAPI.Shipped.txt` per package, enforced by `PublicApiAnalyzers`), so a breaking change
is a deliberate, reviewed diff rather than an accident. See the
[roadmap](https://github.com/AlexeyShirshov/nextorm/milestones) and the
[documentation](https://alexeyshirshov.github.io/nextorm/) for details.

## License

[MIT](https://github.com/AlexeyShirshov/nextorm/blob/main/LICENSE)
