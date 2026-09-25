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
  [`Prepare()`](https://alexeyshirshov.github.io/nextorm/guide/15-query-reuse.html).
* **Entities are optional** — query tables and columns directly with
  [`TableAlias`](https://alexeyshirshov.github.io/nextorm/guide/01-querying-and-projections.html), or map with attributes / fluent API.
* **Rich projection targets** — classes, interfaces, records, anonymous types, tuples, scalars and nested projections.
* **No change tracking** — lean, allocation-conscious materialization built for read-heavy data access.
* **Six database engines** — SQLite, SQL Server, PostgreSQL, MySQL, MariaDB, ClickHouse, plus a built-in in-memory provider.

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

## Quick start

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.Data.Sqlite;
using NextORM.Core;
using NextORM.Sqlite;

var connection = new SqliteConnection("Data Source=:memory:");
connection.Open();

using var builder = new DataContextBuilder().UseSqlite(connection);
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
[Query reuse: cache vs Prepare](https://alexeyshirshov.github.io/nextorm/guide/15-query-reuse.html).

## Benchmarks

Benchmark conditions: provider SQLite (`Microsoft.Data.Sqlite`); async queries; each cell is the
fastest method for that ORM in the linked report. Machine: AMD Ryzen 7 5800HS (16 logical / 8
physical cores), .NET 10.0.12, BenchmarkDotNet 0.15.8 (`ShortRun` + `InProcessEmitToolchain`).

| Method | Nextorm | Dapper | linq2db | EF Core |
|---|:---:|:---:|:---:|:---:|
| [Data fetch](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/nextorm.benchmark.SqliteBenchmarkIteration-report-github.md) | 11.24 μs | 15.82 μs | 17.49 μs | 38.00 μs |
| [Wide data fetch](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/nextorm.benchmark.SqliteBenchmarkLargeIteration-report-github.md) | 8.592 ms | 9.226 ms | 10.350 ms | 10.395 ms |
| [Where](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/nextorm.benchmark.SqliteBenchmarkWhere-report-github.md) | 942.3 μs | 1,385.0 μs | 2,759.0 μs | 3,344.6 μs |
| [Simulate work](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/nextorm.benchmark.SqliteBenchmarkSimulateWork-report-github.md) | 7.003 ms | 25.836 ms | 429.853 ms | 117.813 ms |
| [Any](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/nextorm.benchmark.SqliteBenchmarkAny-report-github.md) | 943.2 μs | 1,401.8 μs | 2,805.2 μs | 3,653.8 μs |
| [FirstOrDefault](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/nextorm.benchmark.SqliteBenchmarkFirst-report-github.md) | 94.77 μs | 146.92 μs | 674.83 μs | 359.09 μs |
| [SingleOrDefault](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/nextorm.benchmark.SqliteBenchmarkSingle-report-github.md) | 95.10 μs | 148.47 μs | 686.56 μs | 370.82 μs |

The full comparison — capabilities and benchmarks, with methodology — is in the
[docs](https://alexeyshirshov.github.io/nextorm/comparisons/).

## Status

Alpha — the public API is still changing and backward compatibility is not preserved between
prereleases. See the [roadmap](https://github.com/AlexeyShirshov/nextorm/milestones) and the
[documentation](https://alexeyshirshov.github.io/nextorm/) for details.

## License

[MIT](https://github.com/AlexeyShirshov/nextorm/blob/main/LICENSE)
