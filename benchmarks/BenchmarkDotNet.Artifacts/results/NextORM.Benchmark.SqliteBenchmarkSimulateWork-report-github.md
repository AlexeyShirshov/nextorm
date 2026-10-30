```

BenchmarkDotNet v0.15.8, Linux Ubuntu 24.04.5 LTS (Noble Numbat)
AMD EPYC 9V74 3.69GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.403
  [Host] : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4

Job=ShortRun  Runtime=.NET 10.0  Toolchain=InProcessEmitToolchain  
IterationCount=3  LaunchCount=1  WarmupCount=3  

```
| Method                              | Mean       | Ratio | Gen0      | Gen1     | Allocated | Alloc Ratio |
|------------------------------------ |-----------:|------:|----------:|---------:|----------:|------------:|
| Nextorm_Prepared_AsyncStream        |   7.024 ms |  0.15 |  390.6250 |        - |   6.25 MB |        1.00 |
| Dapper_AsyncStream                  |  20.144 ms |  0.44 |  531.2500 |        - |    8.9 MB |        1.42 |
| Nextorm_Cached_AsyncStream          |  37.660 ms |  0.83 | 2000.0000 |        - |  32.73 MB |        5.23 |
| Nextorm_Prepared_ToListAsync        |  45.435 ms |  1.00 |  363.6364 |        - |   6.25 MB |        1.00 |
| Nextorm_PreparedForLoop_ToListAsync |  45.641 ms |  1.00 |  363.6364 |        - |   6.27 MB |        1.00 |
| Dapper_Async                        |  59.383 ms |  1.31 |  500.0000 |        - |   8.91 MB |        1.42 |
| Nextorm_Cached_ToList               |  76.829 ms |  1.69 | 2000.0000 | 142.8571 |  32.04 MB |        5.12 |
| EFCore_Compiled_ToListAsync         |  77.044 ms |  1.70 |  857.1429 | 142.8571 |  13.68 MB |        2.19 |
| Linq2Db_ToListAsync                 | 235.186 ms |  5.18 | 7000.0000 |        - | 122.26 MB |       19.55 |
