```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.3 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=.NET 10.0  Runtime=.NET 10.0  

```
| Method                    | Mean | Error | Ratio | RatioSD | Alloc Ratio |
|-------------------------- |-----:|------:|------:|--------:|------------:|
| NextormPreparedSync       |   NA |    NA |     ? |       ? |           ? |
| NextormPreparedSyncToList |   NA |    NA |     ? |       ? |           ? |
| NextormCached             |   NA |    NA |     ? |       ? |           ? |
| NextormCachedSync         |   NA |    NA |     ? |       ? |           ? |
| Linq                      |   NA |    NA |     ? |       ? |           ? |
| LinqToList                |   NA |    NA |     ? |       ? |           ? |

Benchmarks with issues:
  InMemoryBenchmarkIteration.NextormPreparedSync: .NET 10.0(Runtime=.NET 10.0)
  InMemoryBenchmarkIteration.NextormPreparedSyncToList: .NET 10.0(Runtime=.NET 10.0)
  InMemoryBenchmarkIteration.NextormCached: .NET 10.0(Runtime=.NET 10.0)
  InMemoryBenchmarkIteration.NextormCachedSync: .NET 10.0(Runtime=.NET 10.0)
  InMemoryBenchmarkIteration.Linq: .NET 10.0(Runtime=.NET 10.0)
  InMemoryBenchmarkIteration.LinqToList: .NET 10.0(Runtime=.NET 10.0)
