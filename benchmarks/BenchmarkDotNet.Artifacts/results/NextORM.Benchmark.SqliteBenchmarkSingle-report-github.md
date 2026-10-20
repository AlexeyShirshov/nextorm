```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method                           | Mean      | Error     | StdDev   | Ratio | RatioSD | Gen0    | Gen1   | Allocated | Alloc Ratio |
|--------------------------------- |----------:|----------:|---------:|------:|--------:|--------:|-------:|----------:|------------:|
| Nextorm_Prepared_SingleOrDefault |  90.87 μs |  4.734 μs | 0.260 μs |  1.00 |    0.00 |  0.9766 |      - |   8.36 KB |        1.00 |
| Dapper_SingleOrDefault           | 140.79 μs |  7.160 μs | 0.392 μs |  1.55 |    0.01 |  1.9531 |      - |   16.4 KB |        1.96 |
| Nextorm_Cached_SingleOrDefault   | 169.79 μs | 13.813 μs | 0.757 μs |  1.87 |    0.01 |  6.1035 |      - |  51.26 KB |        6.13 |
| EFCore_Compiled_SingleOrDefault  | 345.20 μs | 40.351 μs | 2.212 μs |  3.80 |    0.02 |  9.7656 | 4.8828 |   80.4 KB |        9.62 |
| Linq2Db_Compiled_SingleOrDefault | 394.50 μs | 42.143 μs | 2.310 μs |  4.34 |    0.02 | 22.9492 |      - | 187.82 KB |       22.47 |
| Linq2Db_SingleOrDefault          | 639.64 μs | 46.733 μs | 2.562 μs |  7.04 |    0.03 | 26.3672 |      - |  222.5 KB |       26.61 |
| EFCore_SingleOrDefault           | 702.34 μs |  7.384 μs | 0.405 μs |  7.73 |    0.02 | 18.5547 | 5.8594 | 152.62 KB |       18.25 |
