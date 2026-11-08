```

BenchmarkDotNet v0.15.8, Linux Ubuntu 26.04.1 LTS (Resolute Raccoon)
AMD EPYC 9V74 3.69GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.403
  [Host] : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method                                | Mean      | Error     | StdDev    | Gen0   | Gen1   | Allocated |
|-------------------------------------- |----------:|----------:|----------:|-------:|-------:|----------:|
| Nextorm_PreparedManualSql_ToListAsync |  9.981 μs | 0.4333 μs | 0.0238 μs | 0.0458 |      - |     976 B |
| Nextorm_Prepared_ToListAsync          | 10.031 μs | 1.1394 μs | 0.0625 μs | 0.0458 |      - |     976 B |
| Nextorm_Prepared_AsyncStream          | 10.214 μs | 2.2849 μs | 0.1252 μs | 0.0610 |      - |    1064 B |
| Nextorm_CachedManualSql_ToListAsync   | 10.985 μs | 0.3608 μs | 0.0198 μs | 0.1373 |      - |    2432 B |
| Nextorm_Cached_ToListAsync            | 11.125 μs | 1.2711 μs | 0.0697 μs | 0.1373 |      - |    2384 B |
| DapperAsync                           | 13.313 μs | 0.1019 μs | 0.0056 μs | 0.1068 |      - |    1904 B |
| Dapper_AsyncStream                    | 13.432 μs | 0.2339 μs | 0.0128 μs | 0.1068 |      - |    1856 B |
| Linq2Db_Compiled_ToList               | 13.744 μs | 0.1095 μs | 0.0060 μs | 0.1221 |      - |    2088 B |
| Linq2Db_AsyncStream                   | 13.910 μs | 0.4605 μs | 0.0252 μs | 0.1373 |      - |    2472 B |
| Linq2Db_ToListAsync                   | 13.972 μs | 0.4320 μs | 0.0237 μs | 0.1526 |      - |    2792 B |
| EFCore_Compiled_ToListAsync           | 26.878 μs | 0.4918 μs | 0.0270 μs | 0.6104 | 0.3052 |   10224 B |
