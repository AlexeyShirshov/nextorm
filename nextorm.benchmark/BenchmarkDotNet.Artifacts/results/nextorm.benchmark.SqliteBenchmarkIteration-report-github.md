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
| Nextorm_Prepared_ToListAsync          | 32.97 μs | 0.459 μs | 0.429 μs | 0.3052 |      - |   1.66 KB |
| Nextorm_CachedManualSql_ToListAsync   | 34.35 μs | 0.459 μs | 0.429 μs | 0.5493 |      - |   2.53 KB |
| Nextorm_Cached_ToListAsync            | 34.51 μs | 0.588 μs | 0.550 μs | 0.4883 |      - |   2.41 KB |
| Nextorm_Prepared_AsyncStream          | 34.84 μs | 0.695 μs | 1.271 μs | 0.3052 |      - |   1.63 KB |
| Nextorm_PreparedManualSql_ToListAsync | 38.39 μs | 0.251 μs | 0.234 μs | 0.3052 |      - |   1.66 KB |
| Dapper_AsyncStream                    | 47.47 μs | 0.394 μs | 0.368 μs | 0.3662 |      - |    1.8 KB |
| DapperAsync                           | 47.71 μs | 0.428 μs | 0.379 μs | 0.3662 |      - |   1.88 KB |
| EFCore_Compiled_ToListAsync           | 57.95 μs | 0.474 μs | 0.443 μs | 1.5259 | 0.4883 |   7.19 KB |
