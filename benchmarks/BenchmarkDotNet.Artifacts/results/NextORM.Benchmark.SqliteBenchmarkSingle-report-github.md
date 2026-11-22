```

BenchmarkDotNet v0.15.8, Linux Ubuntu 26.04.1 LTS (Resolute Raccoon)
INTEL XEON PLATINUM 8573C 3.59GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v4

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method                           | Mean      | Error     | StdDev   | Ratio | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|--------------------------------- |----------:|----------:|---------:|------:|--------:|-------:|-------:|----------:|------------:|
| Nextorm_Prepared_SingleOrDefault |  38.78 μs | 22.414 μs | 1.229 μs |  1.00 |    0.04 | 0.0610 |      - |   8.28 KB |        1.00 |
| Dapper_SingleOrDefault           |  62.55 μs |  3.883 μs | 0.213 μs |  1.61 |    0.04 | 0.1221 |      - |  16.04 KB |        1.94 |
| Nextorm_Cached_SingleOrDefault   |  72.83 μs | 15.708 μs | 0.861 μs |  1.88 |    0.05 | 0.4883 |      - |  51.25 KB |        6.19 |
| Linq2Db_Compiled_SingleOrDefault | 139.63 μs |  9.901 μs | 0.543 μs |  3.60 |    0.10 | 1.9531 |      - | 159.77 KB |       19.29 |
| Linq2Db_SingleOrDefault          | 190.26 μs |  4.074 μs | 0.223 μs |  4.91 |    0.13 | 2.1973 |      - | 192.96 KB |       23.30 |
| EFCore_Compiled_SingleOrDefault  | 207.11 μs | 43.136 μs | 2.364 μs |  5.34 |    0.15 | 0.7324 | 0.4883 |  76.02 KB |        9.18 |
| EFCore_SingleOrDefault           | 299.90 μs |  6.814 μs | 0.373 μs |  7.74 |    0.21 | 0.9766 |      - | 146.13 KB |       17.65 |
