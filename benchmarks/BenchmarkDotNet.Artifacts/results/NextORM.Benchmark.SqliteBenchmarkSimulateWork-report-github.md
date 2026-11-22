```

BenchmarkDotNet v0.15.8, Linux Ubuntu 26.04.1 LTS (Resolute Raccoon)
INTEL XEON PLATINUM 8573C 3.59GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v4

Job=ShortRun  Runtime=.NET 11.0  Toolchain=InProcessEmitToolchain  
IterationCount=3  LaunchCount=1  WarmupCount=3  

```
| Method                              | Mean       | Ratio | Gen0      | Allocated | Alloc Ratio |
|------------------------------------ |-----------:|------:|----------:|----------:|------------:|
| Nextorm_Prepared_AsyncStream        |   6.736 ms |  0.32 |   70.3125 |   6.21 MB |        1.00 |
| Dapper_AsyncStream                  |  17.782 ms |  0.85 |   93.7500 |   8.86 MB |        1.43 |
| Nextorm_Prepared_ToListAsync        |  20.860 ms |  1.00 |   62.5000 |   6.22 MB |        1.00 |
| Nextorm_PreparedForLoop_ToListAsync |  21.027 ms |  1.01 |   62.5000 |   6.23 MB |        1.00 |
| Dapper_Async                        |  32.155 ms |  1.54 |   62.5000 |   8.87 MB |        1.43 |
| Nextorm_Cached_AsyncStream          |  32.874 ms |  1.58 |  375.0000 |  30.49 MB |        4.91 |
| Nextorm_Cached_ToList               |  48.695 ms |  2.33 |  363.6364 |  29.79 MB |        4.79 |
| EFCore_Compiled_ToListAsync         |  49.607 ms |  2.38 |   90.9091 |  13.56 MB |        2.18 |
| Linq2Db_ToListAsync                 | 121.737 ms |  5.84 | 1000.0000 | 107.45 MB |       17.29 |
