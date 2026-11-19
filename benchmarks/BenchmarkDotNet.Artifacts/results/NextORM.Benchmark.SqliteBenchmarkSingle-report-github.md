```

BenchmarkDotNet v0.15.8, Linux Ubuntu 26.04.1 LTS (Resolute Raccoon)
AMD EPYC 9V45 4.26GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v4

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method                           | Mean      | Error     | StdDev   | Ratio | RatioSD | Gen0    | Gen1   | Allocated | Alloc Ratio |
|--------------------------------- |----------:|----------:|---------:|------:|--------:|--------:|-------:|----------:|------------:|
| Nextorm_Prepared_SingleOrDefault |  72.72 μs | 12.342 μs | 0.677 μs |  1.00 |    0.01 |  0.4883 |      - |   8.28 KB |        1.00 |
| Dapper_SingleOrDefault           |  90.03 μs | 24.865 μs | 1.363 μs |  1.24 |    0.02 |  0.9766 |      - |  16.04 KB |        1.94 |
| Nextorm_Cached_SingleOrDefault   | 100.50 μs |  9.018 μs | 0.494 μs |  1.38 |    0.01 |  2.9297 |      - |  51.17 KB |        6.18 |
| Linq2Db_Compiled_SingleOrDefault | 150.76 μs | 39.959 μs | 2.190 μs |  2.07 |    0.03 |  9.7656 |      - | 159.77 KB |       19.29 |
| Linq2Db_SingleOrDefault          | 181.76 μs | 62.883 μs | 3.447 μs |  2.50 |    0.05 | 11.7188 |      - | 192.96 KB |       23.30 |
| EFCore_Compiled_SingleOrDefault  | 186.52 μs | 36.939 μs | 2.025 μs |  2.56 |    0.03 |  4.6387 | 2.1973 |  76.02 KB |        9.18 |
| EFCore_SingleOrDefault           | 252.01 μs | 56.202 μs | 3.081 μs |  3.47 |    0.05 |  8.7891 | 4.3945 | 144.96 KB |       17.50 |
