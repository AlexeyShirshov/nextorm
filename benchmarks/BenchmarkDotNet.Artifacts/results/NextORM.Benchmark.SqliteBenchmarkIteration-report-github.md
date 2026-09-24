```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3


```
| Method                                | Mean     | Error    | StdDev   | Gen0   | Gen1   | Allocated |
|-------------------------------------- |---------:|---------:|---------:|-------:|-------:|----------:|
| Nextorm_PreparedManualSql_ToListAsync | 10.38 μs | 0.066 μs | 0.055 μs | 0.1221 |      - |    1048 B |
| Nextorm_Prepared_AsyncStream          | 10.43 μs | 0.071 μs | 0.067 μs | 0.0916 |      - |     792 B |
| Nextorm_Prepared_ToListAsync          | 10.48 μs | 0.064 μs | 0.053 μs | 0.1221 |      - |    1048 B |
| Nextorm_Cached_ToListAsync            | 11.14 μs | 0.079 μs | 0.070 μs | 0.2594 |      - |    2224 B |
| Nextorm_CachedManualSql_ToListAsync   | 11.54 μs | 0.208 μs | 0.213 μs | 0.2747 |      - |    2344 B |
| DapperAsync                           | 15.34 μs | 0.264 μs | 0.343 μs | 0.2136 |      - |    1904 B |
| Linq2Db_Compiled_ToList               | 15.68 μs | 0.146 μs | 0.137 μs | 0.2441 |      - |    2088 B |
| Dapper_AsyncStream                    | 15.71 μs | 0.116 μs | 0.097 μs | 0.2136 |      - |    1856 B |
| Linq2Db_AsyncStream                   | 15.97 μs | 0.189 μs | 0.177 μs | 0.2747 |      - |    2472 B |
| Linq2Db_ToListAsync                   | 16.46 μs | 0.126 μs | 0.118 μs | 0.3052 |      - |    2792 B |
| EFCore_Compiled_ToListAsync           | 39.33 μs | 0.656 μs | 0.614 μs | 1.2207 | 0.6104 |   10224 B |
