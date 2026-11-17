```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v3

Toolchain=InProcessEmitToolchain  

```
| Method                           | Mean     | Error   | StdDev  | Ratio | RatioSD | Gen0    | Gen1   | Allocated | Alloc Ratio |
|--------------------------------- |---------:|--------:|--------:|------:|--------:|--------:|-------:|----------:|------------:|
| Nextorm_Prepared_SingleOrDefault | 107.2 μs | 1.60 μs | 1.42 μs |  1.00 |    0.02 |  0.9766 |      - |   8.28 KB |        1.00 |
| Dapper_SingleOrDefault           | 148.6 μs | 1.17 μs | 1.04 μs |  1.39 |    0.02 |  1.9531 |      - |  16.04 KB |        1.94 |
| Nextorm_Cached_SingleOrDefault   | 177.7 μs | 1.28 μs | 1.19 μs |  1.66 |    0.02 |  5.8594 |      - |  51.18 KB |        6.18 |
| EFCore_Compiled_SingleOrDefault  | 337.2 μs | 6.45 μs | 6.03 μs |  3.15 |    0.07 |  9.2773 | 4.3945 |  76.03 KB |        9.18 |
| Linq2Db_Compiled_SingleOrDefault | 370.8 μs | 3.46 μs | 3.06 μs |  3.46 |    0.05 | 19.5313 |      - | 159.77 KB |       19.29 |
| Linq2Db_SingleOrDefault          | 597.7 μs | 4.44 μs | 4.16 μs |  5.58 |    0.08 | 23.4375 |      - | 192.97 KB |       23.30 |
| EFCore_SingleOrDefault           | 690.5 μs | 5.47 μs | 5.11 μs |  6.44 |    0.10 | 17.5781 | 5.8594 | 147.78 KB |       17.84 |
