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
| Nextorm_Prepared_AsyncStream        |   7.594 ms |  0.16 |   781.2500 |        - |   6.25 MB |        1.00 |
| Dapper_AsyncStream                  |  26.432 ms |  0.55 |  1093.7500 |        - |    8.9 MB |        1.42 |
| Nextorm_Prepared_ToListAsync        |  48.168 ms |  1.00 |   727.2727 |  90.9091 |   6.25 MB |        1.00 |
| Nextorm_PreparedForLoop_ToListAsync |  48.209 ms |  1.00 |   727.2727 |  90.9091 |   6.26 MB |        1.00 |
| Nextorm_Cached_AsyncStream          |  53.813 ms |  1.12 |  4000.0000 |        - |  32.73 MB |        5.24 |
| Dapper_Async                        |  71.695 ms |  1.49 |  1000.0000 | 125.0000 |   8.91 MB |        1.43 |
| Nextorm_Cached_ToList               | 107.427 ms |  2.23 |  4000.0000 | 200.0000 |  32.04 MB |        5.13 |
| EFCore_Compiled_ToListAsync         | 110.812 ms |  2.30 |  1600.0000 | 200.0000 |  13.68 MB |        2.19 |
| Linq2Db_ToListAsync                 | 393.986 ms |  8.18 | 15000.0000 |        - | 121.57 MB |       19.45 |
