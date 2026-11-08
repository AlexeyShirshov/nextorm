```

BenchmarkDotNet v0.15.8, Linux Ubuntu 26.04.1 LTS (Resolute Raccoon)
AMD EPYC 9V74 3.69GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.403
  [Host] : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method                           | Mean      | Error     | StdDev   | Ratio | RatioSD | Gen0    | Gen1   | Allocated | Alloc Ratio |
|--------------------------------- |----------:|----------:|---------:|------:|--------:|--------:|-------:|----------:|------------:|
| Nextorm_Prepared_SingleOrDefault |  89.13 μs |  7.876 μs | 0.432 μs |  1.00 |    0.01 |  0.4883 |      - |   8.36 KB |        1.00 |
| Dapper_SingleOrDefault           | 119.99 μs |  2.630 μs | 0.144 μs |  1.35 |    0.01 |  0.9766 |      - |   16.4 KB |        1.96 |
| Nextorm_Cached_SingleOrDefault   | 128.26 μs | 11.860 μs | 0.650 μs |  1.44 |    0.01 |  2.9297 |      - |  55.55 KB |        6.65 |
| Linq2Db_Compiled_SingleOrDefault | 220.66 μs | 11.740 μs | 0.644 μs |  2.48 |    0.01 | 11.4746 |      - | 187.58 KB |       22.44 |
| EFCore_Compiled_SingleOrDefault  | 243.46 μs | 16.421 μs | 0.900 μs |  2.73 |    0.01 |  4.8828 | 2.4414 |   80.4 KB |        9.62 |
| Linq2Db_SingleOrDefault          | 336.49 μs | 30.527 μs | 1.673 μs |  3.78 |    0.02 | 13.1836 |      - | 221.01 KB |       26.44 |
| EFCore_SingleOrDefault           | 403.82 μs | 13.446 μs | 0.737 μs |  4.53 |    0.02 |  8.7891 | 4.3945 | 149.64 KB |       17.90 |
