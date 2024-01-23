```

BenchmarkDotNet v0.13.10, Windows 11 (10.0.22621.3007/22H2/2022Update/SunValley2)
Intel Core i5-9600KF CPU 3.70GHz (Coffee Lake), 1 CPU, 6 logical and 6 physical cores
.NET SDK 8.0.101
  [Host]   : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2
  .NET 8.0 : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2

Job=.NET 8.0  Runtime=.NET 8.0  

```
| Method                                | Mean     | Error    | StdDev   | Gen0   | Gen1   | Allocated |
|-------------------------------------- |---------:|---------:|---------:|-------:|-------:|----------:|
| Nextorm_Prepared_ToListAsync          | 33.07 μs | 0.364 μs | 0.322 μs | 0.3052 |      - |   1.66 KB |
| Nextorm_PreparedManualSql_ToListAsync | 33.16 μs | 0.262 μs | 0.232 μs | 0.3052 |      - |   1.66 KB |
| Nextorm_Prepared_AsyncStream          | 33.61 μs | 0.388 μs | 0.363 μs | 0.3052 |      - |   1.48 KB |
| Nextorm_Cached_ToListAsync            | 34.65 μs | 0.346 μs | 0.324 μs | 0.4883 |      - |    2.5 KB |
| Nextorm_CachedManualSql_ToListAsync   | 34.66 μs | 0.276 μs | 0.245 μs | 0.5493 |      - |   2.62 KB |
| Dapper_AsyncStream                    | 41.93 μs | 0.540 μs | 0.505 μs | 0.3662 |      - |    1.8 KB |
| DapperAsync                           | 45.08 μs | 0.365 μs | 0.342 μs | 0.3662 |      - |   1.88 KB |
| EFCore_Compiled_ToListAsync           | 57.92 μs | 0.486 μs | 0.431 μs | 1.5259 | 0.4883 |   7.19 KB |
