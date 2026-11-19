```

BenchmarkDotNet v0.15.8, Linux Ubuntu 26.04.1 LTS (Resolute Raccoon)
AMD EPYC 9V45 4.28GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v4

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method                                | Mean      | Error     | StdDev    | Gen0   | Gen1   | Allocated |
|-------------------------------------- |----------:|----------:|----------:|-------:|-------:|----------:|
| Nextorm_PreparedManualSql_ToListAsync |  8.248 μs | 0.7405 μs | 0.0406 μs | 0.0458 |      - |     968 B |
| Nextorm_Prepared_ToListAsync          |  8.373 μs | 1.5991 μs | 0.0877 μs | 0.0458 |      - |     968 B |
| Nextorm_Prepared_AsyncStream          |  8.470 μs | 3.3607 μs | 0.1842 μs | 0.0610 |      - |    1048 B |
| Nextorm_Cached_ToListAsync            |  9.019 μs | 0.4825 μs | 0.0264 μs | 0.1221 |      - |    2248 B |
| Nextorm_CachedManualSql_ToListAsync   |  9.335 μs | 6.2830 μs | 0.3444 μs | 0.1221 |      - |    2288 B |
| DapperAsync                           | 10.785 μs | 2.0802 μs | 0.1140 μs | 0.1068 |      - |    1896 B |
| Dapper_AsyncStream                    | 10.848 μs | 4.4396 μs | 0.2433 μs | 0.1068 |      - |    1848 B |
| Linq2Db_Compiled_ToList               | 11.084 μs | 1.2886 μs | 0.0706 μs | 0.1221 |      - |    2080 B |
| Linq2Db_AsyncStream                   | 11.132 μs | 2.9731 μs | 0.1630 μs | 0.1373 |      - |    2456 B |
| Linq2Db_ToListAsync                   | 11.447 μs | 7.4304 μs | 0.4073 μs | 0.1526 |      - |    2768 B |
| EFCore_Compiled_ToListAsync           | 21.973 μs | 3.1409 μs | 0.1722 μs | 0.5798 | 0.2747 |    9776 B |
