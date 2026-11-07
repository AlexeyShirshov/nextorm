```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method                                | Mean     | Error     | StdDev   | Gen0   | Gen1   | Allocated |
|-------------------------------------- |---------:|----------:|---------:|-------:|-------:|----------:|
| Nextorm_Prepared_ToListAsync          | 10.89 μs |  1.299 μs | 0.071 μs | 0.1068 |      - |     976 B |
| Nextorm_PreparedManualSql_ToListAsync | 11.00 μs |  1.646 μs | 0.090 μs | 0.1068 |      - |     976 B |
| Nextorm_Prepared_AsyncStream          | 11.17 μs |  6.421 μs | 0.352 μs | 0.1221 |      - |    1064 B |
| Nextorm_CachedManualSql_ToListAsync   | 12.38 μs |  2.206 μs | 0.121 μs | 0.2899 |      - |    2432 B |
| Nextorm_Cached_ToListAsync            | 12.42 μs |  2.825 μs | 0.155 μs | 0.2747 |      - |    2384 B |
| Dapper_AsyncStream                    | 15.85 μs |  1.957 μs | 0.107 μs | 0.2136 |      - |    1856 B |
| DapperAsync                           | 16.01 μs |  0.483 μs | 0.026 μs | 0.2136 |      - |    1904 B |
| Linq2Db_AsyncStream                   | 16.66 μs |  2.462 μs | 0.135 μs | 0.2747 |      - |    2472 B |
| Linq2Db_Compiled_ToList               | 16.80 μs |  1.057 μs | 0.058 μs | 0.2441 |      - |    2088 B |
| Linq2Db_ToListAsync                   | 17.20 μs |  1.578 μs | 0.087 μs | 0.3052 |      - |    2792 B |
| EFCore_Compiled_ToListAsync           | 39.88 μs | 19.735 μs | 1.082 μs | 1.2207 | 0.6104 |   10224 B |
