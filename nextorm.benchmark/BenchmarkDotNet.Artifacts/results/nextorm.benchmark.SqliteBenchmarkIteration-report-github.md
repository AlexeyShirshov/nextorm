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
| Nextorm_PreparedManualSql_ToListAsync | 33.49 μs | 0.665 μs | 0.766 μs | 0.3052 |      - |   1.66 KB |
| Nextorm_Cached_ToListAsync            | 36.06 μs | 0.706 μs | 0.785 μs | 0.6104 |      - |   2.91 KB |
| Nextorm_Prepared_ToListAsync          | 39.38 μs | 0.775 μs | 0.892 μs | 0.3052 |      - |   1.66 KB |
| Nextorm_Prepared_AsyncStream          | 39.85 μs | 0.729 μs | 0.682 μs | 0.3052 |      - |   1.48 KB |
| Dapper_AsyncStream                    | 41.85 μs | 0.622 μs | 0.551 μs | 0.3662 |      - |    1.8 KB |
| Nextorm_CachedManualSql_ToListAsync   | 42.32 μs | 0.824 μs | 0.809 μs | 0.6104 |      - |   3.02 KB |
| DapperAsync                           | 42.57 μs | 0.829 μs | 1.018 μs | 0.3662 |      - |   1.88 KB |
| EFCore_Compiled_ToListAsync           | 58.78 μs | 1.084 μs | 1.014 μs | 1.5259 | 0.4883 |   7.19 KB |
