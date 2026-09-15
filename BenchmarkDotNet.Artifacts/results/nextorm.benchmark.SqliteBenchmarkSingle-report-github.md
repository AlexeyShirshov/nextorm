```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.3 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.401
  [Host]    : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  .NET 10.0 : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=.NET 10.0  Runtime=.NET 10.0  

```
| Method                           | Mean     | Error     | StdDev    | Ratio | RatioSD | Gen0    | Allocated | Alloc Ratio |
|--------------------------------- |---------:|----------:|----------:|------:|--------:|--------:|----------:|------------:|
| Nextorm_Prepared_SingleOrDefault | 8.740 ms | 0.1699 ms | 0.2743 ms |  1.00 |    0.04 |       - |    4.3 KB |        1.00 |
| Dapper_SingleOrDefault           | 8.836 ms | 0.1729 ms | 0.3161 ms |  1.01 |    0.05 |       - |   16.4 KB |        3.82 |
| Nextorm_Cached_SingleOrDefault   | 9.046 ms | 0.1322 ms | 0.1172 ms |  1.04 |    0.04 |       - |  40.58 KB |        9.44 |
| EFCore_Compiled_SingleOrDefault  | 9.397 ms | 0.0836 ms | 0.0698 ms |  1.08 |    0.04 |       - |   79.8 KB |       18.57 |
| EFCore_SingleOrDefault           | 9.984 ms | 0.1921 ms | 0.1797 ms |  1.14 |    0.04 | 15.6250 | 149.05 KB |       34.69 |
