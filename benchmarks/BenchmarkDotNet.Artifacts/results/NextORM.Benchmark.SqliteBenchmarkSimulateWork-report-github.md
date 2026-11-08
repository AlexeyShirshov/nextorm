```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Runtime=.NET 10.0  Toolchain=InProcessEmitToolchain  
IterationCount=3  LaunchCount=1  WarmupCount=3  

```
| Method                              | Mean       | Ratio | Gen0       | Gen1     | Allocated | Alloc Ratio |
|------------------------------------ |-----------:|------:|-----------:|---------:|----------:|------------:|
| Nextorm_Prepared_AsyncStream        |   7.871 ms |  0.16 |   781.2500 |        - |   6.25 MB |        1.00 |
| Dapper_AsyncStream                  |  25.427 ms |  0.51 |  1093.7500 |        - |    8.9 MB |        1.42 |
| Nextorm_Prepared_ToListAsync        |  50.268 ms |  1.00 |   727.2727 |  90.9091 |   6.25 MB |        1.00 |
| Nextorm_PreparedForLoop_ToListAsync |  50.479 ms |  1.00 |   727.2727 |  90.9091 |   6.27 MB |        1.00 |
| Nextorm_Cached_AsyncStream          |  61.569 ms |  1.23 |  4000.0000 |        - |  33.08 MB |        5.29 |
| Dapper_Async                        |  70.003 ms |  1.39 |  1000.0000 | 125.0000 |   8.91 MB |        1.42 |
| EFCore_Compiled_ToListAsync         | 114.500 ms |  2.28 |  1600.0000 | 200.0000 |  13.68 MB |        2.19 |
| Nextorm_Cached_ToList               | 117.970 ms |  2.35 |  4000.0000 | 200.0000 |  32.38 MB |        5.18 |
| Linq2Db_ToListAsync                 | 390.157 ms |  7.77 | 15000.0000 |        - | 121.57 MB |       19.44 |
