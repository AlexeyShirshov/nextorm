```

BenchmarkDotNet v0.15.8, Linux Ubuntu 26.04.1 LTS (Resolute Raccoon)
AMD EPYC 7763 3.22GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v3

Job=ShortRun  Runtime=.NET 11.0  Toolchain=InProcessEmitToolchain  
IterationCount=3  LaunchCount=1  WarmupCount=3  

```
| Method                              | Mean       | Ratio | Gen0      | Gen1     | Allocated | Alloc Ratio |
|------------------------------------ |-----------:|------:|----------:|---------:|----------:|------------:|
| Nextorm_Prepared_AsyncStream        |   9.128 ms |  0.19 |  375.0000 |        - |   6.21 MB |        1.00 |
| Dapper_AsyncStream                  |  26.874 ms |  0.55 |  531.2500 |        - |   8.86 MB |        1.43 |
| Nextorm_Prepared_ToListAsync        |  48.467 ms |  1.00 |  363.6364 |        - |   6.22 MB |        1.00 |
| Nextorm_PreparedForLoop_ToListAsync |  48.961 ms |  1.01 |  363.6364 |        - |   6.23 MB |        1.00 |
| Dapper_Async                        |  70.169 ms |  1.45 |  500.0000 |        - |   8.87 MB |        1.43 |
| Nextorm_Cached_AsyncStream          |  73.348 ms |  1.51 | 1000.0000 |        - |  30.34 MB |        4.88 |
| EFCore_Compiled_ToListAsync         | 109.857 ms |  2.27 |  800.0000 |        - |  13.57 MB |        2.18 |
| Nextorm_Cached_ToList               | 118.996 ms |  2.46 | 1800.0000 | 200.0000 |  29.64 MB |        4.77 |
| Linq2Db_ToListAsync                 | 392.032 ms |  8.09 | 6000.0000 |        - | 107.45 MB |       17.29 |
