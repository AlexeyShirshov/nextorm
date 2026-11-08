```

BenchmarkDotNet v0.15.8, Linux Ubuntu 26.04.1 LTS (Resolute Raccoon)
AMD EPYC 9V74 3.68GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.403
  [Host] : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4

Job=ShortRun  Runtime=.NET 10.0  Toolchain=InProcessEmitToolchain  
IterationCount=3  LaunchCount=1  WarmupCount=3  

```
| Method                              | Mean       | Ratio | Gen0      | Gen1     | Allocated | Alloc Ratio |
|------------------------------------ |-----------:|------:|----------:|---------:|----------:|------------:|
| Nextorm_Prepared_AsyncStream        |   6.622 ms |  0.15 |  390.6250 |        - |   6.25 MB |        1.00 |
| Dapper_AsyncStream                  |  19.412 ms |  0.43 |  531.2500 |        - |    8.9 MB |        1.42 |
| Nextorm_Cached_AsyncStream          |  35.040 ms |  0.77 | 2066.6667 |        - |  33.07 MB |        5.29 |
| Nextorm_Prepared_ToListAsync        |  45.322 ms |  1.00 |  363.6364 |        - |   6.25 MB |        1.00 |
| Nextorm_PreparedForLoop_ToListAsync |  45.328 ms |  1.00 |  363.6364 |        - |   6.27 MB |        1.00 |
| Dapper_Async                        |  59.009 ms |  1.30 |  555.5556 | 111.1111 |   8.91 MB |        1.42 |
| Nextorm_Cached_ToList               |  74.941 ms |  1.65 | 2000.0000 | 142.8571 |  32.38 MB |        5.18 |
| EFCore_Compiled_ToListAsync         |  77.219 ms |  1.70 |  857.1429 | 142.8571 |  13.68 MB |        2.19 |
| Linq2Db_ToListAsync                 | 225.819 ms |  4.98 | 7333.3333 | 333.3333 | 121.57 MB |       19.44 |
