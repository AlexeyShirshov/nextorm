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
| Nextorm_Prepared_ToListAsync          | 33.20 μs | 0.638 μs | 0.597 μs | 0.3052 |      - |   1.66 KB |
| Nextorm_PreparedManualSql_ToListAsync | 33.23 μs | 0.588 μs | 0.550 μs | 0.3052 |      - |   1.66 KB |
| Nextorm_CachedManualSql_ToListAsync   | 34.16 μs | 0.320 μs | 0.299 μs | 0.5493 |      - |   2.63 KB |
| Nextorm_Prepared_AsyncStream          | 34.19 μs | 0.433 μs | 0.362 μs | 0.3052 |      - |   1.63 KB |
| Nextorm_Cached_ToListAsync            | 34.35 μs | 0.683 μs | 0.839 μs | 0.4883 |      - |   2.52 KB |
| Dapper_AsyncStream                    | 42.52 μs | 0.840 μs | 0.825 μs | 0.3662 |      - |    1.8 KB |
| DapperAsync                           | 42.55 μs | 0.727 μs | 0.680 μs | 0.3662 |      - |   1.88 KB |
| EFCore_Compiled_ToListAsync           | 57.70 μs | 0.403 μs | 0.337 μs | 1.5259 | 0.4883 |   7.19 KB |
