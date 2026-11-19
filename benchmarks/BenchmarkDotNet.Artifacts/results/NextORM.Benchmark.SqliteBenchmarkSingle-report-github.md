```

BenchmarkDotNet v0.15.8, Linux Ubuntu 26.04.1 LTS (Resolute Raccoon)
AMD EPYC 7763 3.23GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method                           | Mean      | Error      | StdDev   | Ratio | RatioSD | Gen0    | Gen1   | Allocated | Alloc Ratio |
|--------------------------------- |----------:|-----------:|---------:|------:|--------:|--------:|-------:|----------:|------------:|
| Nextorm_Prepared_SingleOrDefault |  95.42 μs |   3.346 μs | 0.183 μs |  1.00 |    0.00 |  0.4883 |      - |   8.28 KB |        1.00 |
| Dapper_SingleOrDefault           | 143.43 μs |  17.065 μs | 0.935 μs |  1.50 |    0.01 |  0.9766 |      - |  16.04 KB |        1.94 |
| Nextorm_Cached_SingleOrDefault   | 178.41 μs |  18.140 μs | 0.994 μs |  1.87 |    0.01 |  2.9297 |      - |  50.71 KB |        6.12 |
| Linq2Db_Compiled_SingleOrDefault | 372.28 μs |  28.032 μs | 1.537 μs |  3.90 |    0.02 |  9.7656 |      - | 159.77 KB |       19.29 |
| EFCore_Compiled_SingleOrDefault  | 375.85 μs |  49.288 μs | 2.702 μs |  3.94 |    0.03 |  4.3945 | 1.9531 |  76.03 KB |        9.18 |
| Linq2Db_SingleOrDefault          | 649.65 μs | 168.732 μs | 9.249 μs |  6.81 |    0.08 | 11.7188 |      - | 194.53 KB |       23.49 |
| EFCore_SingleOrDefault           | 749.53 μs | 128.974 μs | 7.069 μs |  7.86 |    0.07 |  7.8125 | 3.9063 | 148.09 KB |       17.88 |
