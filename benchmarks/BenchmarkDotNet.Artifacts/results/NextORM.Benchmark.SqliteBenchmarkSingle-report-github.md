```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v3

Toolchain=InProcessEmitToolchain  

```
| Method                           | Mean     | Error    | StdDev   | Median   | Ratio | RatioSD | Gen0    | Gen1   | Allocated | Alloc Ratio |
|--------------------------------- |---------:|---------:|---------:|---------:|------:|--------:|--------:|-------:|----------:|------------:|
| Nextorm_Prepared_SingleOrDefault | 107.4 μs |  0.50 μs |  0.44 μs | 107.4 μs |  1.00 |    0.01 |  0.9766 |      - |   8.28 KB |        1.00 |
| Dapper_SingleOrDefault           | 150.6 μs |  3.00 μs |  3.21 μs | 149.8 μs |  1.40 |    0.03 |  1.9531 |      - |  16.04 KB |        1.94 |
| Nextorm_Cached_SingleOrDefault   | 207.1 μs |  4.10 μs |  8.84 μs | 207.1 μs |  1.93 |    0.08 |  5.8594 |      - |  51.18 KB |        6.18 |
| EFCore_Compiled_SingleOrDefault  | 350.6 μs |  5.64 μs |  5.00 μs | 350.5 μs |  3.26 |    0.05 |  9.2773 | 4.3945 |  76.03 KB |        9.18 |
| Linq2Db_Compiled_SingleOrDefault | 359.6 μs |  3.22 μs |  2.85 μs | 358.8 μs |  3.35 |    0.03 | 19.5313 |      - | 159.77 KB |       19.29 |
| Linq2Db_SingleOrDefault          | 578.0 μs |  2.74 μs |  2.43 μs | 577.8 μs |  5.38 |    0.03 | 23.4375 |      - | 192.97 KB |       23.30 |
| EFCore_SingleOrDefault           | 711.8 μs | 13.34 μs | 37.19 μs | 693.7 μs |  6.63 |    0.35 | 17.5781 | 5.8594 | 144.34 KB |       17.43 |
