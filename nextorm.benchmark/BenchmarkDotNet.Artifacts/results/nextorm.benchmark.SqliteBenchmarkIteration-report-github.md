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
| Nextorm_Prepared_ToListAsync          | 33.21 μs | 0.615 μs | 0.575 μs | 0.3052 |      - |   1.66 KB |
| Nextorm_PreparedManualSql_ToListAsync | 33.28 μs | 0.601 μs | 0.562 μs | 0.3052 |      - |   1.66 KB |
| Nextorm_Prepared_AsyncStream          | 34.44 μs | 0.646 μs | 0.634 μs | 0.3052 |      - |   1.63 KB |
| Nextorm_Cached_ToListAsync            | 34.48 μs | 0.603 μs | 0.564 μs | 0.4883 |      - |   2.34 KB |
| Nextorm_CachedManualSql_ToListAsync   | 34.72 μs | 0.594 μs | 0.556 μs | 0.4883 |      - |   2.46 KB |
| Dapper_AsyncStream                    | 42.46 μs | 0.833 μs | 0.779 μs | 0.3662 |      - |    1.8 KB |
| DapperAsync                           | 47.85 μs | 0.640 μs | 0.599 μs | 0.3662 |      - |   1.88 KB |
| EFCore_Compiled_ToListAsync           | 57.56 μs | 0.417 μs | 0.348 μs | 1.5259 | 0.4883 |   7.19 KB |
