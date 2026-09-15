```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.3 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.401
  [Host]    : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  .NET 10.0 : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=.NET 10.0  Runtime=.NET 10.0  

```
| Method           | Mean      | Error     | StdDev    | Gen0    | Allocated |
|----------------- |----------:|----------:|----------:|--------:|----------:|
| Nextorm_Cached   |        NA |        NA |        NA |      NA |        NA |
| Nextorm_Prepared |  9.137 ms | 0.0895 ms | 0.0837 ms |       - |   9.26 KB |
| EFCore_Compiled  |  9.914 ms | 0.0982 ms | 0.0918 ms |       - |  120.7 KB |
| Dapper           | 10.057 ms | 0.1759 ms | 0.2161 ms |       - |  21.45 KB |
| EFCore           | 10.632 ms | 0.1297 ms | 0.1012 ms | 15.6250 | 178.67 KB |

Benchmarks with issues:
  SqliteBenchmarkJoin.Nextorm_Cached: .NET 10.0(Runtime=.NET 10.0)
