```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3


```
| Method                                | Mean     | Error    | StdDev   | Gen0   | Gen1   | Allocated |
|-------------------------------------- |---------:|---------:|---------:|-------:|-------:|----------:|
| Nextorm_Prepared_AsyncStream          | 13.98 μs | 0.275 μs | 0.317 μs | 0.0916 |      - |     792 B |
| Nextorm_PreparedManualSql_ToListAsync | 14.31 μs | 0.281 μs | 0.500 μs | 0.1221 |      - |    1048 B |
| Nextorm_Prepared_ToListAsync          | 14.45 μs | 0.287 μs | 0.659 μs | 0.1221 |      - |    1048 B |
| Nextorm_Cached_ToListAsync            | 15.40 μs | 0.303 μs | 0.653 μs | 0.2747 |      - |    2480 B |
| Nextorm_CachedManualSql_ToListAsync   | 16.15 μs | 0.321 μs | 0.792 μs | 0.2747 |      - |    2528 B |
| DapperAsync                           | 21.40 μs | 0.422 μs | 0.890 μs | 0.2136 |      - |    1904 B |
| Linq2Db_Compiled_ToList               | 22.12 μs | 0.442 μs | 0.701 μs | 0.2441 |      - |    2088 B |
| Linq2Db_AsyncStream                   | 23.38 μs | 0.463 μs | 0.977 μs | 0.2747 |      - |    2472 B |
| Dapper_AsyncStream                    | 23.47 μs | 0.467 μs | 1.334 μs | 0.2136 |      - |    1856 B |
| Linq2Db_ToListAsync                   | 25.38 μs | 0.507 μs | 1.059 μs | 0.3052 |      - |    2792 B |
| EFCore_Compiled_ToListAsync           | 60.19 μs | 1.366 μs | 3.985 μs | 1.2207 | 0.6104 |   10224 B |
