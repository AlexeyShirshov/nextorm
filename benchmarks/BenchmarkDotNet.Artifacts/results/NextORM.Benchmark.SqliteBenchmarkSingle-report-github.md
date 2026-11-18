```

BenchmarkDotNet v0.15.8, Linux Ubuntu 26.04.1 LTS (Resolute Raccoon)
AMD EPYC 7763 2.61GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method                           | Mean      | Error      | StdDev    | Ratio | RatioSD | Gen0    | Gen1   | Allocated | Alloc Ratio |
|--------------------------------- |----------:|-----------:|----------:|------:|--------:|--------:|-------:|----------:|------------:|
| Nextorm_Prepared_SingleOrDefault |  94.96 μs |   2.340 μs |  0.128 μs |  1.00 |    0.00 |  0.4883 |      - |   8.28 KB |        1.00 |
| Dapper_SingleOrDefault           | 140.19 μs |   8.247 μs |  0.452 μs |  1.48 |    0.00 |  0.9766 |      - |  16.04 KB |        1.94 |
| Nextorm_Cached_SingleOrDefault   | 172.18 μs |  21.405 μs |  1.173 μs |  1.81 |    0.01 |  2.9297 |      - |  51.18 KB |        6.18 |
| EFCore_Compiled_SingleOrDefault  | 357.43 μs |   3.767 μs |  0.206 μs |  3.76 |    0.00 |  4.3945 | 1.9531 |  76.03 KB |        9.18 |
| Linq2Db_Compiled_SingleOrDefault | 358.53 μs |   5.353 μs |  0.293 μs |  3.78 |    0.01 |  9.7656 |      - | 159.77 KB |       19.29 |
| Linq2Db_SingleOrDefault          | 609.89 μs |  71.241 μs |  3.905 μs |  6.42 |    0.04 | 11.7188 |      - | 192.96 KB |       23.30 |
| EFCore_SingleOrDefault           | 737.96 μs | 276.451 μs | 15.153 μs |  7.77 |    0.14 |  7.8125 | 3.9063 | 144.96 KB |       17.50 |
