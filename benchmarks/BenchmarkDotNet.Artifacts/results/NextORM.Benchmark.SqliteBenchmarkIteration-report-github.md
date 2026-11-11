```

BenchmarkDotNet v0.15.8, Linux Ubuntu 26.04.1 LTS (Resolute Raccoon)
AMD EPYC 7763 3.02GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method                                | Mean     | Error    | StdDev   | Gen0   | Gen1   | Allocated |
|-------------------------------------- |---------:|---------:|---------:|-------:|-------:|----------:|
| Nextorm_PreparedManualSql_ToListAsync | 10.75 μs | 0.597 μs | 0.033 μs | 0.0458 |      - |     968 B |
| Nextorm_Prepared_ToListAsync          | 10.83 μs | 3.559 μs | 0.195 μs | 0.0458 |      - |     968 B |
| Nextorm_Prepared_AsyncStream          | 10.95 μs | 6.472 μs | 0.355 μs | 0.0610 |      - |    1048 B |
| Nextorm_Cached_ToListAsync            | 12.16 μs | 1.070 μs | 0.059 μs | 0.1221 |      - |    2224 B |
| Nextorm_CachedManualSql_ToListAsync   | 12.19 μs | 0.406 μs | 0.022 μs | 0.1221 |      - |    2264 B |
| Dapper_AsyncStream                    | 15.80 μs | 1.695 μs | 0.093 μs | 0.0916 |      - |    1848 B |
| DapperAsync                           | 15.81 μs | 2.288 μs | 0.125 μs | 0.0916 |      - |    1896 B |
| Linq2Db_Compiled_ToList               | 16.50 μs | 3.612 μs | 0.198 μs | 0.1221 |      - |    2080 B |
| Linq2Db_AsyncStream                   | 16.66 μs | 1.711 μs | 0.094 μs | 0.1221 |      - |    2456 B |
| Linq2Db_ToListAsync                   | 16.89 μs | 0.464 μs | 0.025 μs | 0.1526 |      - |    2768 B |
| EFCore_Compiled_ToListAsync           | 38.46 μs | 0.301 μs | 0.016 μs | 0.5493 | 0.2441 |    9776 B |
