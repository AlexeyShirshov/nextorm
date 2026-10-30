```

BenchmarkDotNet v0.15.8, Linux Ubuntu 24.04.5 LTS (Noble Numbat)
AMD EPYC 9V74 3.69GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.403
  [Host] : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method                                | Mean      | Error     | StdDev    | Gen0   | Gen1   | Allocated |
|-------------------------------------- |----------:|----------:|----------:|-------:|-------:|----------:|
| Nextorm_PreparedManualSql_ToListAsync |  9.926 μs | 0.2159 μs | 0.0118 μs | 0.0610 |      - |   1.02 KB |
| Nextorm_Prepared_ToListAsync          |  9.989 μs | 0.4221 μs | 0.0231 μs | 0.0610 |      - |   1.02 KB |
| Nextorm_Prepared_AsyncStream          | 10.024 μs | 1.7178 μs | 0.0942 μs | 0.0610 |      - |   1.04 KB |
| Nextorm_CachedManualSql_ToListAsync   | 10.881 μs | 0.3057 μs | 0.0168 μs | 0.1373 |      - |   2.39 KB |
| Nextorm_Cached_ToListAsync            | 10.945 μs | 0.2314 μs | 0.0127 μs | 0.1373 |      - |   2.37 KB |
| DapperAsync                           | 13.107 μs | 0.4271 μs | 0.0234 μs | 0.1068 |      - |   1.86 KB |
| Dapper_AsyncStream                    | 13.140 μs | 0.5434 μs | 0.0298 μs | 0.1068 |      - |   1.81 KB |
| Linq2Db_Compiled_ToList               | 13.492 μs | 0.2956 μs | 0.0162 μs | 0.1221 |      - |   2.04 KB |
| Linq2Db_AsyncStream                   | 13.701 μs | 0.1448 μs | 0.0079 μs | 0.1373 |      - |   2.41 KB |
| Linq2Db_ToListAsync                   | 13.788 μs | 0.3371 μs | 0.0185 μs | 0.1526 |      - |   2.73 KB |
| EFCore_Compiled_ToListAsync           | 26.482 μs | 2.1830 μs | 0.1197 μs | 0.6104 | 0.3052 |   9.98 KB |
