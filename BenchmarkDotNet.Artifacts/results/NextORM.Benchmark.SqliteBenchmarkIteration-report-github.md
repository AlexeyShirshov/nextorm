```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3


```
| Method                                | Mean     | Error    | StdDev   | Gen0   | Gen1   | Allocated |
|-------------------------------------- |---------:|---------:|---------:|-------:|-------:|----------:|
| Nextorm_Prepared_ToListAsync          | 13.67 μs | 0.272 μs | 0.227 μs | 0.1221 |      - |    1048 B |
| Nextorm_PreparedManualSql_ToListAsync | 13.73 μs | 0.259 μs | 0.229 μs | 0.1221 |      - |    1048 B |
| Nextorm_Prepared_AsyncStream          | 13.82 μs | 0.273 μs | 0.281 μs | 0.0916 |      - |     792 B |
| Nextorm_CachedManualSql_ToListAsync   | 15.55 μs | 0.308 μs | 0.622 μs | 0.2441 |      - |    2536 B |
| Nextorm_Cached_ToListAsync            | 15.58 μs | 0.291 μs | 0.358 μs | 0.2441 |      - |    2488 B |
| Dapper_AsyncStream                    | 20.07 μs | 0.400 μs | 0.411 μs | 0.1831 |      - |    1856 B |
| DapperAsync                           | 20.13 μs | 0.395 μs | 0.762 μs | 0.1831 |      - |    1904 B |
| Linq2Db_Compiled_ToList               | 21.38 μs | 0.416 μs | 0.526 μs | 0.2441 |      - |    2088 B |
| Linq2Db_AsyncStream                   | 21.87 μs | 0.436 μs | 0.552 μs | 0.2441 |      - |    2472 B |
| Linq2Db_ToListAsync                   | 22.13 μs | 0.409 μs | 0.560 μs | 0.3052 |      - |    2792 B |
| EFCore_Compiled_ToListAsync           | 51.52 μs | 0.968 μs | 0.994 μs | 1.2207 | 0.6104 |   10224 B |
