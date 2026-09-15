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
| Nextorm_Prepared_SingleOrDefault | 8.649 ms | 0.1726 ms | 0.4426 ms |  1.00 |    0.07 |       - |    4.3 KB |        1.00 |
| Dapper_SingleOrDefault           | 9.045 ms | 0.1797 ms | 0.4201 ms |  1.05 |    0.07 |       - |   16.4 KB |        3.82 |
| EFCore_Compiled_SingleOrDefault  | 9.245 ms | 0.1392 ms | 0.1162 ms |  1.07 |    0.06 |       - |   79.8 KB |       18.57 |
| Nextorm_Cached_SingleOrDefault   | 9.305 ms | 0.1796 ms | 0.3668 ms |  1.08 |    0.07 |       - |  40.58 KB |        9.44 |
| EFCore_SingleOrDefault           | 9.832 ms | 0.1830 ms | 0.1429 ms |  1.14 |    0.06 | 15.6250 | 149.05 KB |       34.69 |
