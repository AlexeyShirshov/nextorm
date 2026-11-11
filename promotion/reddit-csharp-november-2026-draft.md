I've been working on [Nextorm](https://github.com/AlexeyShirshov/nextorm), an open-source ORM for .NET, for quite a while. I've just reached an important milestone: **the first stable release, version 1.0.9**.

The idea is to combine Dapper-like performance with strongly typed C# queries and automatic SQL generation, using an expression-based API rather than a standard LINQ provider. Nextorm supports six database engines, CTEs, window functions, and direct JSON/CSV streaming without materializing a DTO per row.

Here are a few results from my published SQLite async benchmarks (lower is better):

| Scenario | Nextorm | Dapper | linq2db | EF Core |
|---|---:|---:|---:|---:|
| [Data fetch](https://github.com/AlexeyShirshov/nextorm/blob/publish/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkIteration-report-github.md) | **10.29 μs** | 14.89 μs | 15.76 μs | 36.64 μs |
| [Wide data fetch](https://github.com/AlexeyShirshov/nextorm/blob/publish/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkLargeIteration-report-github.md) | **8.331 ms** | 8.680 ms | 9.366 ms | 9.771 ms |
| [Where](https://github.com/AlexeyShirshov/nextorm/blob/publish/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkWhere-report-github.md) | **886.7 μs** | 1,369.6 μs | 1,412.3 μs | 3,199.6 μs |

Each cell uses that ORM's fastest measured method, so the execution modes aren't identical (Nextorm uses prepared queries). SQLite, .NET 10.0.12, Ryzen 7 5800HS, BenchmarkDotNet ShortRun, October 2026. [More benchmarks and methodology](https://alexeyshirshov.github.io/nextorm/comparisons/).

I'd love feedback from C# developers: how does the API look to you, and where would you use something like this?

*AI disclosure: I wrote the original description in Russian and used AI to help translate, edit, and shorten it.*
