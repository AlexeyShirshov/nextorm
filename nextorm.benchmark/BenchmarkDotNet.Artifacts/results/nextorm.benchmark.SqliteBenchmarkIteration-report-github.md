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
| Nextorm_PreparedManualSql_ToListAsync | 32.98 μs | 0.658 μs | 0.676 μs | 0.3052 |      - |   1.66 KB |
| Nextorm_Prepared_ToListAsync          | 33.36 μs | 0.635 μs | 0.624 μs | 0.3052 |      - |   1.66 KB |
| Nextorm_Prepared_AsyncStream          | 34.34 μs | 0.617 μs | 0.578 μs | 0.3052 |      - |   1.63 KB |
| Nextorm_Cached_ToListAsync            | 34.42 μs | 0.636 μs | 0.595 μs | 0.4883 |      - |   2.36 KB |
| Nextorm_CachedManualSql_ToListAsync   | 39.87 μs | 0.284 μs | 0.252 μs | 0.4883 |      - |   2.48 KB |
| Dapper_AsyncStream                    | 42.37 μs | 0.827 μs | 0.885 μs | 0.3662 |      - |    1.8 KB |
| DapperAsync                           | 45.71 μs | 0.890 μs | 0.832 μs | 0.3662 |      - |   1.88 KB |
| EFCore_Compiled_ToListAsync           | 57.88 μs | 0.678 μs | 0.634 μs | 1.5259 | 0.4883 |   7.19 KB |
