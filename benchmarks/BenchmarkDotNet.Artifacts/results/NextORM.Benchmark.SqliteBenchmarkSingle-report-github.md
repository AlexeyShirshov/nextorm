```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3


```
| Method                           | Mean      | Error    | StdDev    | Ratio | RatioSD | Gen0    | Gen1   | Allocated | Alloc Ratio |
|--------------------------------- |----------:|---------:|----------:|------:|--------:|--------:|-------:|----------:|------------:|
| Nextorm_Prepared_SingleOrDefault |  93.45 μs | 1.503 μs |  1.332 μs |  1.00 |    0.02 |  0.9766 |      - |   8.36 KB |        1.00 |
| Dapper_SingleOrDefault           | 142.91 μs | 1.918 μs |  1.794 μs |  1.53 |    0.03 |  1.9531 |      - |   16.4 KB |        1.96 |
| Nextorm_Cached_SingleOrDefault   | 164.10 μs | 2.231 μs |  2.087 μs |  1.76 |    0.03 |  7.8125 |      - |  64.77 KB |        7.75 |
| EFCore_Compiled_SingleOrDefault  | 356.16 μs | 6.745 μs | 11.454 μs |  3.81 |    0.13 |  9.7656 | 4.8828 |   80.4 KB |        9.62 |
| Linq2Db_Compiled_SingleOrDefault | 369.17 μs | 5.505 μs |  5.150 μs |  3.95 |    0.08 | 22.9492 |      - | 187.58 KB |       22.44 |
| Linq2Db_SingleOrDefault          | 591.50 μs | 4.677 μs |  3.905 μs |  6.33 |    0.10 | 23.4375 |      - | 222.41 KB |       26.61 |
| EFCore_SingleOrDefault           | 679.25 μs | 8.021 μs |  7.110 μs |  7.27 |    0.12 | 17.5781 | 5.8594 | 149.64 KB |       17.90 |
