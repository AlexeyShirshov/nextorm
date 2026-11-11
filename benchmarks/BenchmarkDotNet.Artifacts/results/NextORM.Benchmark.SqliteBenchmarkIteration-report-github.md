```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method                                | Mean     | Error     | StdDev   | Gen0   | Gen1   | Allocated |
|-------------------------------------- |---------:|----------:|---------:|-------:|-------:|----------:|
| Nextorm_PreparedManualSql_ToListAsync | 10.26 μs |  1.028 μs | 0.056 μs | 0.1068 |      - |     968 B |
| Nextorm_Prepared_ToListAsync          | 10.59 μs |  1.833 μs | 0.100 μs | 0.1068 |      - |     968 B |
| Nextorm_Prepared_AsyncStream          | 10.78 μs |  1.144 μs | 0.063 μs | 0.1221 |      - |    1048 B |
| Nextorm_CachedManualSql_ToListAsync   | 11.91 μs |  0.708 μs | 0.039 μs | 0.2594 |      - |    2264 B |
| Nextorm_Cached_ToListAsync            | 12.08 μs |  1.405 μs | 0.077 μs | 0.2594 |      - |    2224 B |
| DapperAsync                           | 14.59 μs |  0.907 μs | 0.050 μs | 0.2136 |      - |    1896 B |
| Dapper_AsyncStream                    | 15.00 μs |  2.495 μs | 0.137 μs | 0.2136 |      - |    1848 B |
| Linq2Db_Compiled_ToList               | 15.54 μs |  2.219 μs | 0.122 μs | 0.2441 |      - |    2080 B |
| Linq2Db_AsyncStream                   | 15.84 μs |  1.189 μs | 0.065 μs | 0.2747 |      - |    2456 B |
| Linq2Db_ToListAsync                   | 16.00 μs |  2.846 μs | 0.156 μs | 0.3052 |      - |    2768 B |
| EFCore_Compiled_ToListAsync           | 34.36 μs | 11.011 μs | 0.604 μs | 1.1597 | 0.5493 |    9776 B |
