```

BenchmarkDotNet v0.15.8, Linux Ubuntu 26.04.1 LTS (Resolute Raccoon)
AMD EPYC 7763 2.71GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v3

Job=ShortRun  Runtime=.NET 11.0  Toolchain=InProcessEmitToolchain  
IterationCount=3  LaunchCount=1  WarmupCount=3  

```
| Method                              | Mean       | Ratio | Gen0      | Gen1     | Allocated | Alloc Ratio |
|------------------------------------ |-----------:|------:|----------:|---------:|----------:|------------:|
| Nextorm_Prepared_AsyncStream        |   8.610 ms |  0.18 |  375.0000 |        - |   6.21 MB |        1.00 |
| Dapper_AsyncStream                  |  27.766 ms |  0.58 |  531.2500 |        - |   8.86 MB |        1.43 |
| Nextorm_Prepared_ToListAsync        |  48.004 ms |  1.00 |  363.6364 |        - |   6.22 MB |        1.00 |
| Nextorm_PreparedForLoop_ToListAsync |  48.734 ms |  1.02 |  363.6364 |        - |   6.23 MB |        1.00 |
| Nextorm_Cached_AsyncStream          |  65.605 ms |  1.37 | 1000.0000 |        - |  30.15 MB |        4.85 |
| Dapper_Async                        |  70.316 ms |  1.46 |  500.0000 |        - |   8.87 MB |        1.43 |
| EFCore_Compiled_ToListAsync         | 109.613 ms |  2.28 |  800.0000 |        - |  13.57 MB |        2.18 |
| Nextorm_Cached_ToList               | 111.655 ms |  2.33 | 1800.0000 | 200.0000 |  29.45 MB |        4.74 |
| Linq2Db_ToListAsync                 | 418.616 ms |  8.72 | 6000.0000 |        - | 107.45 MB |       17.29 |
