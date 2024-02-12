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
| Nextorm_PreparedManualSql_ToListAsync | 33.24 μs | 0.646 μs | 0.692 μs | 0.3052 |      - |   1.66 KB |
| Nextorm_Prepared_ToListAsync          | 33.59 μs | 0.519 μs | 0.486 μs | 0.3052 |      - |   1.66 KB |
| Nextorm_Cached_ToListAsync            | 34.84 μs | 0.679 μs | 0.974 μs | 0.4883 |      - |   2.41 KB |
| Nextorm_CachedManualSql_ToListAsync   | 35.22 μs | 0.671 μs | 0.745 μs | 0.5493 |      - |   2.53 KB |
| Nextorm_Prepared_AsyncStream          | 40.13 μs | 0.795 μs | 0.916 μs | 0.3052 |      - |   1.63 KB |
| DapperAsync                           | 42.28 μs | 0.747 μs | 0.662 μs | 0.3662 |      - |   1.88 KB |
| Dapper_AsyncStream                    | 42.43 μs | 0.831 μs | 0.889 μs | 0.3662 |      - |    1.8 KB |
| EFCore_Compiled_ToListAsync           | 58.74 μs | 1.003 μs | 0.939 μs | 1.5259 | 0.4883 |   7.19 KB |
