```

BenchmarkDotNet v0.15.8, Linux Ubuntu 26.04.1 LTS (Resolute Raccoon)
AMD EPYC 7763 2.70GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method                           | Mean      | Error     | StdDev   | Ratio | RatioSD | Gen0    | Gen1   | Allocated | Alloc Ratio |
|--------------------------------- |----------:|----------:|---------:|------:|--------:|--------:|-------:|----------:|------------:|
| Nextorm_Prepared_SingleOrDefault |  92.38 μs |  5.703 μs | 0.313 μs |  1.00 |    0.00 |  0.4883 |      - |   8.28 KB |        1.00 |
| Dapper_SingleOrDefault           | 142.69 μs |  4.574 μs | 0.251 μs |  1.54 |    0.01 |  0.9766 |      - |  16.04 KB |        1.94 |
| Nextorm_Cached_SingleOrDefault   | 167.27 μs |  1.682 μs | 0.092 μs |  1.81 |    0.01 |  2.9297 |      - |  50.79 KB |        6.13 |
| EFCore_Compiled_SingleOrDefault  | 369.31 μs | 20.391 μs | 1.118 μs |  4.00 |    0.02 |  4.3945 | 1.9531 |  76.03 KB |        9.18 |
| Linq2Db_Compiled_SingleOrDefault | 372.78 μs | 44.549 μs | 2.442 μs |  4.04 |    0.03 |  9.7656 |      - | 159.77 KB |       19.29 |
| Linq2Db_SingleOrDefault          | 644.79 μs | 70.503 μs | 3.864 μs |  6.98 |    0.04 | 11.7188 |      - | 192.96 KB |       23.30 |
| EFCore_SingleOrDefault           | 758.62 μs | 88.165 μs | 4.833 μs |  8.21 |    0.05 |  7.8125 | 3.9063 | 147.93 KB |       17.86 |
