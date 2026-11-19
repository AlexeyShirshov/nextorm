```

BenchmarkDotNet v0.15.8, Linux Ubuntu 26.04.1 LTS (Resolute Raccoon)
AMD EPYC 9V45 4.19GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v4

Job=ShortRun  Runtime=.NET 11.0  Toolchain=InProcessEmitToolchain  
IterationCount=3  LaunchCount=1  WarmupCount=3  

```
| Method                              | Mean       | Ratio | Gen0      | Gen1     | Allocated | Alloc Ratio |
|------------------------------------ |-----------:|------:|----------:|---------:|----------:|------------:|
| Nextorm_Prepared_AsyncStream        |   5.654 ms |  0.15 |  382.8125 |        - |   6.21 MB |        1.00 |
| Dapper_AsyncStream                  |  14.806 ms |  0.40 |  546.8750 |        - |   8.86 MB |        1.43 |
| Nextorm_Cached_AsyncStream          |  26.266 ms |  0.71 | 1906.2500 |  31.2500 |  30.87 MB |        4.97 |
| Nextorm_Prepared_ToListAsync        |  36.913 ms |  1.00 |  384.6154 |  76.9231 |   6.21 MB |        1.00 |
| Nextorm_PreparedForLoop_ToListAsync |  37.768 ms |  1.02 |  357.1429 |  71.4286 |   6.23 MB |        1.00 |
| Dapper_Async                        |  45.972 ms |  1.25 |  545.4545 |  90.9091 |   8.87 MB |        1.43 |
| Nextorm_Cached_ToList               |  57.297 ms |  1.55 | 1888.8889 | 222.2222 |  30.17 MB |        4.85 |
| EFCore_Compiled_ToListAsync         |  59.253 ms |  1.61 |  777.7778 | 111.1111 |  13.56 MB |        2.18 |
| Linq2Db_ToListAsync                 | 107.557 ms |  2.91 | 6600.0000 | 200.0000 | 107.44 MB |       17.29 |
