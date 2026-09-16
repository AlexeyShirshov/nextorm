```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method                           | Mean      | Error      | StdDev    | Ratio | RatioSD | Gen0    | Gen1   | Allocated | Alloc Ratio |
|--------------------------------- |----------:|-----------:|----------:|------:|--------:|--------:|-------:|----------:|------------:|
| Nextorm_Prepared_SingleOrDefault |  95.10 μs |   4.527 μs |  0.248 μs |  1.00 |    0.00 |  0.9766 |      - |   8.36 KB |        1.00 |
| Dapper_SingleOrDefault           | 148.47 μs |  38.734 μs |  2.123 μs |  1.56 |    0.02 |  1.9531 |      - |   16.4 KB |        1.96 |
| Nextorm_Cached_SingleOrDefault   | 157.62 μs |  14.232 μs |  0.780 μs |  1.66 |    0.01 |  5.3711 |      - |  44.55 KB |        5.33 |
| EFCore_Compiled_SingleOrDefault  | 370.82 μs | 269.002 μs | 14.745 μs |  3.90 |    0.13 |  9.7656 | 4.8828 |   80.4 KB |        9.62 |
| Linq2Db_SingleOrDefault          | 686.56 μs | 119.807 μs |  6.567 μs |  7.22 |    0.06 | 26.3672 |      - | 222.27 KB |       26.59 |
| EFCore_SingleOrDefault           | 771.44 μs |  99.311 μs |  5.444 μs |  8.11 |    0.05 | 17.5781 | 5.8594 | 149.65 KB |       17.90 |
