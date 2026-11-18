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
* **NativeAOT** — enable `PublishAot=true` without a separate compiled model or manual query
  registration. Representative scenarios are verified for all six database providers on
  .NET 11 `linux-x64`, and 40 SQLite scenarios also pass on Windows `win-x64`; see the
  [NativeAOT guide](https://alexeyshirshov.github.io/nextorm/infrastructure/05-nativeaot.html).

## Installation

```bash
dotnet add package nextorm
dotnet add package nextorm.sqlite   # pick one provider
```

Use `--prerelease` only if you want to install a preview. Package Manager Console equivalent:
`Install-Package nextorm`.

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

Native binaries run against SQLite, PostgreSQL, SQL Server, MySQL, MariaDB, and ClickHouse on
`net11.0` (`linux-x64`); Windows x64 (`win-x64`) is also verified with 40 SQLite scenarios.
Server providers have not been verified on Windows in that run.
Native-versus-JIT checks cover representative scenarios, including
writes, bulk insert, raw SQL row mapping, streaming, and eager loading. The EF Core bridge
requires JIT. See the [NativeAOT guide](https://alexeyshirshov.github.io/nextorm/infrastructure/05-nativeaot.html)
for the tested scope, JSON serialization requirements, and limitations.

Row materializers are generated at build time for every projection type in the assembly — named
types, record DTOs and anonymous types alike — so a prepared query keeps compiled-class
materialization under AOT instead of falling back to the expression interpreter.

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
method for that ORM in the linked report. Machine: AMD EPYC 9V45 (4 logical / 2 physical cores), .NET 11.0.0,
BenchmarkDotNet 0.15.8 (`ShortRun` + `InProcessEmitToolchain`). As of **2026-11-18**. Each method name links
to its BenchmarkDotNet report, and the **Code** column links to the benchmark source in this repository — the
exact code behind the row. The last twelve rows are the basic-feature scenarios (Category A in the [comparison
article](https://alexeyshirshov.github.io/nextorm/comparisons/benchmarks.html)): Nextorm through `Prepare()`
against the compiled form of EF Core and linq2db and raw Dapper, ten queries per iteration, so each cell there
is the per-query mean. Those rows come from the Category A suite run on **2026-11-18**.
<!-- benchmarks:conditions:end -->

<!-- benchmarks:e2e:start -->
| Method | Nextorm | Dapper | linq2db | EF Core | Code |
|---|:---:|:---:|:---:|:---:|---|
| [Data fetch](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkIteration-report-github.md) | **8.248 μs** | 10.785 μs | 11.084 μs | 21.973 μs | [code](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/nextorm.benchmark/Sqlite/SqliteBenchmarkIteration.cs#L17) |
| [Wide data fetch](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkLargeIteration-report-github.md) | **6.632 ms** | 7.467 ms | 7.585 ms | 7.659 ms | [code](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/nextorm.benchmark/Sqlite/SqliteBenchmarkLargeIteration.cs#L24) |
| [Where](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkWhere-report-github.md) | **705.0 μs** | 896.1 μs | 926.8 μs | 1,924.2 μs | [code](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/nextorm.benchmark/Sqlite/SqliteBenchmarkWhere.cs#L20) |
| [Simulate work](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkSimulateWork-report-github.md) | **5.654 ms** | 14.806 ms | 107.557 ms | 59.253 ms | [code](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/nextorm.benchmark/Sqlite/SqliteBenchmarkSimulteWork.cs#L17) |
| [Any](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkAny-report-github.md) | **728.6 μs** | 921.6 μs | 968.5 μs | 1,950.4 μs | [code](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/nextorm.benchmark/Sqlite/SqliteBenchmarkAny.cs#L17) |
| [FirstOrDefault](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkFirst-report-github.md) | **74.11 μs** | 94.52 μs | 150.02 μs | 200.65 μs | [code](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/nextorm.benchmark/Sqlite/SqliteBenchmarkFirst.cs#L17) |
| [SingleOrDefault](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkSingle-report-github.md) | **72.72 μs** | 90.03 μs | 150.76 μs | 186.52 μs | [code](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/nextorm.benchmark/Sqlite/SqliteBenchmarkSingle.cs#L17) |
| [`CASE WHEN`](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkFeaturesFair-report-github.md) | **7.48 μs** | 9.49 μs | 9.76 μs | 19.71 μs | [code](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/nextorm.benchmark/Sqlite/SqliteBenchmarkFeaturesFair.cs#L398) |
| [CTE](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkFeaturesFair-report-github.md) | **7.79 μs** | 12.33 μs | 12.39 μs | — | [code](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/nextorm.benchmark/Sqlite/SqliteBenchmarkFeaturesFair.cs#L618) |
| [`SELECT DISTINCT`](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkFeaturesFair-report-github.md) | **8.32 μs** | 10.24 μs | 10.58 μs | 20.71 μs | [code](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/nextorm.benchmark/Sqlite/SqliteBenchmarkFeaturesFair.cs#L359) |
| [`EXCEPT`](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkFeaturesFair-report-github.md) | **8.80 μs** | 11.71 μs | 12.65 μs | 23.45 μs | [code](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/nextorm.benchmark/Sqlite/SqliteBenchmarkFeaturesFair.cs#L718) |
| [IN-list](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkFeaturesFair-report-github.md) | **9.09 μs** | 10.77 μs | 16.12 μs | 24.12 μs | [code](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/nextorm.benchmark/Sqlite/SqliteBenchmarkFeaturesFair.cs#L511) |
| [`INTERSECT`](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkFeaturesFair-report-github.md) | **7.94 μs** | 10.45 μs | 11.71 μs | 21.08 μs | [code](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/nextorm.benchmark/Sqlite/SqliteBenchmarkFeaturesFair.cs#L683) |
| [4-table join](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkFeaturesFair-report-github.md) | **10.01 μs** | 16.03 μs | 14.64 μs | 25.06 μs | [code](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/nextorm.benchmark/Sqlite/SqliteBenchmarkFeaturesFair.cs#L315) |
| [`LEFT JOIN`](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkFeaturesFair-report-github.md) | **9.98 μs** | 13.03 μs | 13.02 μs | 22.16 μs | [code](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/nextorm.benchmark/Sqlite/SqliteBenchmarkFeaturesFair.cs#L275) |
| [Recursive CTE](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkFeaturesFair-report-github.md) | **9.08 μs** | 13.44 μs | 13.98 μs | — | [code](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/nextorm.benchmark/Sqlite/SqliteBenchmarkFeaturesFair.cs#L647) |
| [String functions](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkFeaturesFair-report-github.md) | **7.13 μs** | 9.23 μs | 9.27 μs | 18.23 μs | [code](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/nextorm.benchmark/Sqlite/SqliteBenchmarkFeaturesFair.cs#L437) |
| [UDF](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkFeaturesFair-report-github.md) | **7.22 μs** | 9.34 μs | 9.55 μs | 18.57 μs | [code](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/nextorm.benchmark/Sqlite/SqliteBenchmarkFeaturesFair.cs#L757) |
| [Window functions](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkFeaturesFair-report-github.md) | **10.23 μs** | 14.65 μs | 14.60 μs | — | [code](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/nextorm.benchmark/Sqlite/SqliteBenchmarkFeaturesFair.cs#L558) |
<!-- benchmarks:e2e:end -->

The full comparison — capabilities and benchmarks, with methodology — is in the
[docs](https://alexeyshirshov.github.io/nextorm/comparisons/).

## Status

Stable. The packages target .NET 10 and .NET 11. Package validation checks public API compatibility
against a published baseline. See the
[release notes](https://alexeyshirshov.github.io/nextorm/#releases) and
[roadmap](https://github.com/AlexeyShirshov/nextorm/milestones).

## Contributing

Contributions are welcome! [`nextorm.extensions`](src/nextorm.extensions) is designed for
third-party developers to add reusable query operators through Nextorm's public extension APIs.
You can contribute to this package or publish an independent extension package.
See [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

## License

[MIT](https://github.com/AlexeyShirshov/nextorm/blob/main/LICENSE)
