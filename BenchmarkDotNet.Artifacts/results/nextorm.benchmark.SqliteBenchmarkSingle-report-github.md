```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.401
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3


```
| Method                           | Mean      | Error     | StdDev    | Ratio | RatioSD | Gen0    | Gen1   | Allocated | Alloc Ratio |
|--------------------------------- |----------:|----------:|----------:|------:|--------:|--------:|-------:|----------:|------------:|
| Nextorm_Prepared_SingleOrDefault |  92.16 μs |  1.818 μs |  1.701 μs |  1.00 |    0.03 |  0.9766 |      - |   8.36 KB |        1.00 |
| Nextorm_Cached_SingleOrDefault   | 143.59 μs |  2.815 μs |  2.764 μs |  1.56 |    0.04 |  5.3711 |      - |  44.86 KB |        5.37 |
| Dapper_SingleOrDefault           | 144.80 μs |  2.535 μs |  2.372 μs |  1.57 |    0.04 |  1.9531 |      - |   16.4 KB |        1.96 |
| EFCore_Compiled_SingleOrDefault  | 362.22 μs |  5.896 μs |  5.227 μs |  3.93 |    0.09 |  9.7656 | 4.8828 |   80.4 KB |        9.62 |
| Linq2Db_SingleOrDefault          | 624.16 μs | 12.254 μs | 18.342 μs |  6.77 |    0.23 | 23.4375 |      - | 221.01 KB |       26.44 |
| EFCore_SingleOrDefault           | 697.72 μs |  9.410 μs |  8.802 μs |  7.57 |    0.17 | 17.5781 | 5.8594 | 149.64 KB |       17.90 |
