```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3


```
| Method                           | Mean     | Error    | StdDev   | Ratio | RatioSD | Gen0    | Gen1   | Allocated | Alloc Ratio |
|--------------------------------- |---------:|---------:|---------:|------:|--------:|--------:|-------:|----------:|------------:|
| Nextorm_Prepared_SingleOrDefault | 127.1 μs |  2.47 μs |  3.47 μs |  1.00 |    0.04 |  0.9766 |      - |   8.36 KB |        1.00 |
| Dapper_SingleOrDefault           | 198.4 μs |  3.85 μs |  5.00 μs |  1.56 |    0.06 |  1.9531 |      - |   16.4 KB |        1.96 |
| Nextorm_Cached_SingleOrDefault   | 251.3 μs |  5.01 μs | 12.94 μs |  1.98 |    0.11 |  7.8125 |      - |  71.65 KB |        8.57 |
| EFCore_Compiled_SingleOrDefault  | 489.2 μs |  9.17 μs | 15.57 μs |  3.85 |    0.16 |  9.7656 | 4.8828 |   80.4 KB |        9.62 |
| Linq2Db_Compiled_SingleOrDefault | 547.7 μs | 10.80 μs | 23.26 μs |  4.31 |    0.22 | 22.4609 |      - | 187.58 KB |       22.44 |
| Linq2Db_SingleOrDefault          | 825.7 μs | 15.34 μs | 18.84 μs |  6.50 |    0.23 | 23.4375 |      - | 221.01 KB |       26.44 |
| EFCore_SingleOrDefault           | 953.9 μs | 18.60 μs | 42.74 μs |  7.51 |    0.39 | 15.6250 | 3.9063 | 149.64 KB |       17.90 |
