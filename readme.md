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
  .NET 10 / `linux-x64`); see the
  [NativeAOT guide](https://alexeyshirshov.github.io/nextorm/infrastructure/05-nativeaot.html).

## Installation

```bash
dotnet add package nextorm --prerelease
dotnet add package nextorm.sqlite --prerelease   # pick one provider
```

All current releases are prereleases, so `--prerelease` is required. Package Manager Console equivalent:
`Install-Package nextorm -Prerelease`.

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

Partial and experimental: published native binaries run with the SQLite provider on `net10.0`
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
method for that ORM in the linked report. Machine: AMD EPYC 9V74 (4 logical / 2 physical cores), .NET 10.0.14,
BenchmarkDotNet 0.15.8 (`ShortRun` + `InProcessEmitToolchain`). As of **2026-11-08**. The last twelve rows are
the basic-feature scenarios (Category A in the [comparison
article](https://alexeyshirshov.github.io/nextorm/comparisons/benchmarks.html)): Nextorm through `Prepare()`
against the compiled form of EF Core and linq2db and raw Dapper, ten queries per iteration, so each cell there
is the per-query mean. Those rows come from the Category A suite run on **2026-11-08**.
<!-- benchmarks:conditions:end -->

<!-- benchmarks:e2e:start -->
| Method | Nextorm | Dapper | linq2db | EF Core |
|---|:---:|:---:|:---:|:---:|
| [Data fetch](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkIteration-report-github.md) | **9.981 μs** | 13.313 μs | 13.744 μs | 26.878 μs |
| [Wide data fetch](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkLargeIteration-report-github.md) | **7.305 ms** | 7.999 ms | 8.100 ms | 8.562 ms |
| [Where](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkWhere-report-github.md) | **868.8 μs** | 1,156.3 μs | 1,199.4 μs | 2,345.1 μs |
| [Simulate work](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkSimulateWork-report-github.md) | **6.622 ms** | 19.412 ms | 225.819 ms | 77.219 ms |
| [Any](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkAny-report-github.md) | **877.4 μs** | 1,176.8 μs | 1,246.9 μs | 2,457.1 μs |
| [FirstOrDefault](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkFirst-report-github.md) | **87.67 μs** | 121.11 μs | 220.07 μs | 242.83 μs |
| [SingleOrDefault](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkSingle-report-github.md) | **89.13 μs** | 119.99 μs | 220.66 μs | 243.46 μs |
| [`CASE WHEN`](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkFeaturesFair-report-github.md) | **8.82 μs** | 12.20 μs | 12.60 μs | 24.30 μs |
| [CTE](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkFeaturesFair-report-github.md) | **9.17 μs** | 16.60 μs | 16.64 μs | — |
| [`SELECT DISTINCT`](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkFeaturesFair-report-github.md) | **10.19 μs** | 13.87 μs | 13.50 μs | 26.35 μs |
| [`EXCEPT`](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkFeaturesFair-report-github.md) | **10.24 μs** | 14.54 μs | 16.16 μs | 29.38 μs |
| [IN-list](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkFeaturesFair-report-github.md) | **11.17 μs** | 14.00 μs | 24.80 μs | 38.16 μs |
| [`INTERSECT`](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkFeaturesFair-report-github.md) | **9.57 μs** | 13.58 μs | 15.15 μs | 27.98 μs |
| [4-table join](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkFeaturesFair-report-github.md) | **11.95 μs** | 21.18 μs | 18.87 μs | 33.04 μs |
| [`LEFT JOIN`](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkFeaturesFair-report-github.md) | **11.52 μs** | 16.79 μs | 16.99 μs | 29.30 μs |
| [Recursive CTE](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkFeaturesFair-report-github.md) | **11.08 μs** | 17.88 μs | 18.91 μs | — |
| [String functions](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkFeaturesFair-report-github.md) | **8.72 μs** | 11.94 μs | 12.52 μs | 24.14 μs |
| [UDF](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkFeaturesFair-report-github.md) | **8.73 μs** | 11.95 μs | 12.51 μs | 24.12 μs |
| [Window functions](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkFeaturesFair-report-github.md) | **12.46 μs** | 20.43 μs | 21.13 μs | — |
<!-- benchmarks:e2e:end -->

The full comparison — capabilities and benchmarks, with methodology — is in the
[docs](https://alexeyshirshov.github.io/nextorm/comparisons/).

## Status

Beta — the public API is still changing and backward compatibility is not preserved between
prereleases. See the [roadmap](https://github.com/AlexeyShirshov/nextorm/milestones) and the
[documentation](https://alexeyshirshov.github.io/nextorm/) for details.

## Contributing

Contributions are welcome! [`nextorm.extensions`](src/nextorm.extensions) is designed for
third-party developers to add reusable query operators through Nextorm's public extension APIs.
You can contribute to this package or publish an independent extension package.
See [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

## License

[MIT](https://github.com/AlexeyShirshov/nextorm/blob/main/LICENSE)
