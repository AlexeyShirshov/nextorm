```

BenchmarkDotNet v0.15.8, Linux Ubuntu 26.04.1 LTS (Resolute Raccoon)
INTEL XEON PLATINUM 8573C 3.54GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v4

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method                                | Mean      | Error     | StdDev    | Gen0   | Gen1   | Allocated |
|-------------------------------------- |----------:|----------:|----------:|-------:|-------:|----------:|
| Nextorm_PreparedManualSql_ToListAsync |  5.346 μs | 0.2067 μs | 0.0113 μs | 0.0076 |      - |     968 B |
| Nextorm_Prepared_ToListAsync          |  5.381 μs | 0.2363 μs | 0.0130 μs | 0.0076 |      - |     968 B |
| Nextorm_Prepared_AsyncStream          |  5.443 μs | 0.6903 μs | 0.0378 μs | 0.0076 |      - |    1048 B |
| Nextorm_Cached_ToListAsync            |  6.216 μs | 0.5474 μs | 0.0300 μs | 0.0229 |      - |    2248 B |
| Nextorm_CachedManualSql_ToListAsync   |  6.363 μs | 3.6391 μs | 0.1995 μs | 0.0229 |      - |    2288 B |
| DapperAsync                           |  8.326 μs | 0.9659 μs | 0.0529 μs | 0.0153 |      - |    1896 B |
| Dapper_AsyncStream                    |  8.437 μs | 0.8604 μs | 0.0472 μs | 0.0153 |      - |    1848 B |
| Linq2Db_Compiled_ToList               |  8.523 μs | 0.8135 μs | 0.0446 μs | 0.0153 |      - |    2080 B |
| Linq2Db_AsyncStream                   |  8.854 μs | 0.8001 μs | 0.0439 μs | 0.0153 |      - |    2456 B |
| Linq2Db_ToListAsync                   |  8.887 μs | 0.1974 μs | 0.0108 μs | 0.0305 |      - |    2768 B |
| EFCore_Compiled_ToListAsync           | 23.562 μs | 0.9900 μs | 0.0543 μs | 0.0916 | 0.0610 |    9776 B |
