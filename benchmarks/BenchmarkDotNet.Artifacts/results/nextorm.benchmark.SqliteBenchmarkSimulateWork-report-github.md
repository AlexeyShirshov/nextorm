```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Runtime=.NET 10.0  Toolchain=InProcessEmitToolchain  
IterationCount=3  LaunchCount=1  WarmupCount=3  

```
| Method                              | Mean       | Ratio | Gen0       | Gen1     | Allocated | Alloc Ratio |
|------------------------------------ |-----------:|------:|-----------:|---------:|----------:|------------:|
| Nextorm_Prepared_AsyncStream        |   7.003 ms |  0.14 |   781.2500 |        - |   6.24 MB |        1.00 |
| Dapper_AsyncStream                  |  25.836 ms |  0.53 |  1093.7500 |        - |    8.9 MB |        1.42 |
| Nextorm_Prepared_ToListAsync        |  49.044 ms |  1.00 |   727.2727 |  90.9091 |   6.25 MB |        1.00 |
| Nextorm_PreparedForLoop_ToListAsync |  49.755 ms |  1.01 |   700.0000 | 100.0000 |   6.26 MB |        1.00 |
| Nextorm_Cached_AsyncStream          |  57.073 ms |  1.16 |  3777.7778 |        - |  30.55 MB |        4.89 |
| Dapper_Async                        |  73.256 ms |  1.49 |  1000.0000 | 142.8571 |   8.91 MB |        1.43 |
| Nextorm_Cached_ToList               | 115.717 ms |  2.36 |  3600.0000 | 200.0000 |  29.87 MB |        4.78 |
| EFCore_Compiled_ToListAsync         | 117.813 ms |  2.40 |  1600.0000 | 200.0000 |  13.68 MB |        2.19 |
| Linq2Db_ToListAsync                 | 429.853 ms |  8.76 | 15000.0000 |        - | 122.18 MB |       19.56 |
