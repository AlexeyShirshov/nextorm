```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3


```
| Method                           | Mean       | Error     | StdDev    | Median   | Ratio | RatioSD | Gen0    | Gen1   | Allocated | Alloc Ratio |
|--------------------------------- |-----------:|----------:|----------:|---------:|------:|--------:|--------:|-------:|----------:|------------:|
| Nextorm_Prepared_SingleOrDefault |   119.7 μs |   2.37 μs |   3.89 μs | 119.2 μs |  1.00 |    0.05 |  0.9766 |      - |   8.36 KB |        1.00 |
| Dapper_SingleOrDefault           |   188.0 μs |   3.70 μs |   7.04 μs | 187.3 μs |  1.57 |    0.08 |  1.9531 |      - |   16.4 KB |        1.96 |
| Nextorm_Cached_SingleOrDefault   |   199.5 μs |   3.94 μs |   9.35 μs | 198.1 μs |  1.67 |    0.09 |  5.8594 |      - |  52.67 KB |        6.30 |
| EFCore_Compiled_SingleOrDefault  |   471.0 μs |   9.27 μs |  19.96 μs | 469.3 μs |  3.94 |    0.21 |  9.7656 | 3.9063 |   80.4 KB |        9.62 |
| Linq2Db_Compiled_SingleOrDefault |   497.0 μs |   9.81 μs |  24.05 μs | 490.0 μs |  4.16 |    0.24 | 19.5313 |      - | 187.58 KB |       22.44 |
| Linq2Db_SingleOrDefault          |   820.8 μs |  25.47 μs |  68.43 μs | 811.2 μs |  6.86 |    0.61 | 23.4375 |      - | 221.01 KB |       26.44 |
| EFCore_SingleOrDefault           | 1,353.3 μs | 213.66 μs | 629.99 μs | 945.3 μs | 11.32 |    5.26 | 15.6250 | 3.9063 | 149.64 KB |       17.90 |
