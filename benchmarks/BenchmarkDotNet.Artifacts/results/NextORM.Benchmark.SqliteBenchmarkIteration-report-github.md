```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method                                | Mean     | Error    | StdDev   | Gen0   | Gen1   | Allocated |
|-------------------------------------- |---------:|---------:|---------:|-------:|-------:|----------:|
| Nextorm_Prepared_AsyncStream          | 10.29 μs | 0.721 μs | 0.040 μs | 0.0916 |      - |     792 B |
| Nextorm_Prepared_ToListAsync          | 10.36 μs | 0.443 μs | 0.024 μs | 0.1221 |      - |    1048 B |
| Nextorm_PreparedManualSql_ToListAsync | 10.42 μs | 1.585 μs | 0.087 μs | 0.1221 |      - |    1048 B |
| Nextorm_Cached_ToListAsync            | 11.52 μs | 1.287 μs | 0.071 μs | 0.2747 |      - |    2360 B |
| Nextorm_CachedManualSql_ToListAsync   | 11.57 μs | 1.068 μs | 0.059 μs | 0.2747 |      - |    2384 B |
| DapperAsync                           | 14.89 μs | 0.674 μs | 0.037 μs | 0.2136 |      - |    1904 B |
| Dapper_AsyncStream                    | 15.15 μs | 3.006 μs | 0.165 μs | 0.2136 |      - |    1856 B |
| Linq2Db_Compiled_ToList               | 15.76 μs | 3.107 μs | 0.170 μs | 0.2441 |      - |    2088 B |
| Linq2Db_AsyncStream                   | 15.91 μs | 2.317 μs | 0.127 μs | 0.2747 |      - |    2472 B |
| Linq2Db_ToListAsync                   | 16.24 μs | 1.788 μs | 0.098 μs | 0.3052 |      - |    2792 B |
| EFCore_Compiled_ToListAsync           | 36.64 μs | 3.484 μs | 0.191 μs | 1.2207 | 0.6104 |   10224 B |
