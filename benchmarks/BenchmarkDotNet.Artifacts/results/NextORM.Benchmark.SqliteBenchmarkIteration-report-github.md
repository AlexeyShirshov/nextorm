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
| Nextorm_PreparedManualSql_ToListAsync | 10.79 μs | 0.942 μs | 0.052 μs | 0.0458 |      - |     968 B |
| Nextorm_Prepared_ToListAsync          | 10.93 μs | 3.181 μs | 0.174 μs | 0.0458 |      - |     968 B |
| Nextorm_Prepared_AsyncStream          | 11.10 μs | 0.620 μs | 0.034 μs | 0.0610 |      - |    1048 B |
| Nextorm_CachedManualSql_ToListAsync   | 12.45 μs | 0.510 μs | 0.028 μs | 0.1221 |      - |    2288 B |
| Nextorm_Cached_ToListAsync            | 12.54 μs | 3.816 μs | 0.209 μs | 0.1221 |      - |    2248 B |
| DapperAsync                           | 15.79 μs | 3.173 μs | 0.174 μs | 0.0916 |      - |    1896 B |
| Dapper_AsyncStream                    | 16.16 μs | 1.259 μs | 0.069 μs | 0.0916 |      - |    1848 B |
| Linq2Db_AsyncStream                   | 16.66 μs | 3.596 μs | 0.197 μs | 0.1221 |      - |    2456 B |
| Linq2Db_Compiled_ToList               | 16.89 μs | 3.883 μs | 0.213 μs | 0.1221 |      - |    2080 B |
| Linq2Db_ToListAsync                   | 17.29 μs | 3.955 μs | 0.217 μs | 0.1526 |      - |    2768 B |
| EFCore_Compiled_ToListAsync           | 39.29 μs | 7.717 μs | 0.423 μs | 0.5493 | 0.2441 |    9776 B |
