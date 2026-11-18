```

BenchmarkDotNet v0.15.8, Linux Ubuntu 26.04.1 LTS (Resolute Raccoon)
AMD EPYC 7763 3.24GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method                                | Mean     | Error    | StdDev   | Gen0   | Gen1   | Allocated |
|-------------------------------------- |---------:|---------:|---------:|-------:|-------:|----------:|
| Nextorm_Prepared_ToListAsync          | 11.01 μs | 3.812 μs | 0.209 μs | 0.0458 |      - |     968 B |
| Nextorm_PreparedManualSql_ToListAsync | 11.02 μs | 0.096 μs | 0.005 μs | 0.0458 |      - |     968 B |
| Nextorm_Prepared_AsyncStream          | 11.08 μs | 2.377 μs | 0.130 μs | 0.0610 |      - |    1048 B |
| Nextorm_CachedManualSql_ToListAsync   | 12.25 μs | 1.675 μs | 0.092 μs | 0.1221 |      - |    2288 B |
| Nextorm_Cached_ToListAsync            | 12.43 μs | 2.694 μs | 0.148 μs | 0.1221 |      - |    2248 B |
| DapperAsync                           | 15.50 μs | 1.213 μs | 0.066 μs | 0.0916 |      - |    1896 B |
| Dapper_AsyncStream                    | 15.59 μs | 0.662 μs | 0.036 μs | 0.0916 |      - |    1848 B |
| Linq2Db_Compiled_ToList               | 16.17 μs | 1.289 μs | 0.071 μs | 0.1221 |      - |    2080 B |
| Linq2Db_AsyncStream                   | 16.50 μs | 2.474 μs | 0.136 μs | 0.1221 |      - |    2456 B |
| Linq2Db_ToListAsync                   | 16.69 μs | 0.195 μs | 0.011 μs | 0.1526 |      - |    2768 B |
| EFCore_Compiled_ToListAsync           | 38.59 μs | 0.542 μs | 0.030 μs | 0.5493 | 0.2441 |    9776 B |
