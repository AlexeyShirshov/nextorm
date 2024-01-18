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
| Nextorm_Prepared_AsyncStream          | 32.97 μs | 0.658 μs | 0.758 μs | 0.3052 |      - |   1.48 KB |
| Nextorm_Prepared_ToListAsync          | 33.12 μs | 0.657 μs | 0.963 μs | 0.3052 |      - |   1.66 KB |
| Nextorm_PreparedManualSql_ToListAsync | 33.47 μs | 0.547 μs | 0.511 μs | 0.3052 |      - |   1.66 KB |
| Nextorm_CachedManualSql_ToListAsync   | 35.76 μs | 0.714 μs | 0.733 μs | 0.6104 |      - |   3.01 KB |
| Nextorm_Cached_ToListAsync            | 36.05 μs | 0.715 μs | 1.361 μs | 0.6104 |      - |   2.89 KB |
| DapperAsync                           | 40.61 μs | 0.698 μs | 0.619 μs | 0.3662 |      - |   1.88 KB |
| Dapper_AsyncStream                    | 41.30 μs | 0.788 μs | 0.809 μs | 0.3662 |      - |    1.8 KB |
| EFCore_Compiled_ToListAsync           | 57.51 μs | 1.098 μs | 1.027 μs | 1.5259 | 0.4883 |   7.19 KB |
