```

BenchmarkDotNet v0.15.8, Linux Ubuntu 26.04.1 LTS (Resolute Raccoon)
AMD EPYC 7763 2.60GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v3

Job=ShortRun  Runtime=.NET 11.0  Toolchain=InProcessEmitToolchain  
IterationCount=3  LaunchCount=1  WarmupCount=3  

```
| Method                              | Mean       | Ratio | Gen0      | Gen1     | Allocated | Alloc Ratio |
|------------------------------------ |-----------:|------:|----------:|---------:|----------:|------------:|
| Nextorm_Prepared_AsyncStream        |   9.469 ms |  0.19 |  375.0000 |        - |   6.21 MB |        1.00 |
| Dapper_AsyncStream                  |  28.654 ms |  0.58 |  531.2500 |        - |   8.86 MB |        1.43 |
| Nextorm_Prepared_ToListAsync        |  49.334 ms |  1.00 |  363.6364 |        - |   6.22 MB |        1.00 |
| Nextorm_PreparedForLoop_ToListAsync |  49.923 ms |  1.01 |  363.6364 |        - |   6.23 MB |        1.00 |
| Nextorm_Cached_AsyncStream          |  70.823 ms |  1.44 | 1000.0000 |        - |  30.45 MB |        4.90 |
| Dapper_Async                        |  70.859 ms |  1.44 |  428.5714 |        - |   8.87 MB |        1.43 |
| EFCore_Compiled_ToListAsync         | 110.994 ms |  2.25 |  800.0000 |        - |  13.57 MB |        2.18 |
| Nextorm_Cached_ToList               | 122.452 ms |  2.48 | 1800.0000 | 200.0000 |  29.75 MB |        4.79 |
| Linq2Db_ToListAsync                 | 407.227 ms |  8.25 | 6000.0000 |        - | 107.45 MB |       17.29 |
