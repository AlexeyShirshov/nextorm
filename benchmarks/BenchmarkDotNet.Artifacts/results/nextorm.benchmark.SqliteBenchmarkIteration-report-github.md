```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method                                | Mean     | Error     | StdDev   | Gen0   | Gen1   | Allocated |
|-------------------------------------- |---------:|----------:|---------:|-------:|-------:|----------:|
| Nextorm_Prepared_AsyncStream          | 11.24 μs |  2.065 μs | 0.113 μs | 0.0916 |      - |     792 B |
| Nextorm_Prepared_ToListAsync          | 11.51 μs | 10.483 μs | 0.575 μs | 0.1068 |      - |     976 B |
| Nextorm_PreparedManualSql_ToListAsync | 11.80 μs |  3.879 μs | 0.213 μs | 0.1068 |      - |     976 B |
| Nextorm_Cached_ToListAsync            | 11.94 μs |  4.357 μs | 0.239 μs | 0.1831 |      - |    1648 B |
| Nextorm_CachedManualSql_ToListAsync   | 11.95 μs | 13.234 μs | 0.725 μs | 0.1984 |      - |    1768 B |
| DapperAsync                           | 15.82 μs |  0.784 μs | 0.043 μs | 0.2136 |      - |    1904 B |
| Dapper_AsyncStream                    | 16.78 μs | 31.370 μs | 1.719 μs | 0.2136 |      - |    1856 B |
| Linq2Db_AsyncStream                   | 17.49 μs |  4.929 μs | 0.270 μs | 0.2747 |      - |    2472 B |
| Linq2Db_ToListAsync                   | 17.88 μs | 13.727 μs | 0.752 μs | 0.3052 |      - |    2792 B |
| EFCore_Compiled_ToListAsync           | 38.00 μs |  9.478 μs | 0.520 μs | 1.2207 | 0.6104 |   10224 B |
