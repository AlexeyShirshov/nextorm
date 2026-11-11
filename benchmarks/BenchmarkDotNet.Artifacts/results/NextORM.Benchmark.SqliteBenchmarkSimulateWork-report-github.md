```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v3

Job=ShortRun  Runtime=.NET 11.0  Toolchain=InProcessEmitToolchain  
IterationCount=3  LaunchCount=1  WarmupCount=3  

```
| Method                              | Mean       | Ratio | Gen0       | Gen1     | Allocated | Alloc Ratio |
|------------------------------------ |-----------:|------:|-----------:|---------:|----------:|------------:|
| Nextorm_Prepared_AsyncStream        |   7.558 ms |  0.15 |   773.4375 |        - |   6.21 MB |        1.00 |
| Dapper_AsyncStream                  |  24.385 ms |  0.50 |  1093.7500 |        - |   8.86 MB |        1.43 |
| Nextorm_Prepared_ToListAsync        |  48.987 ms |  1.00 |   727.2727 |  90.9091 |   6.22 MB |        1.00 |
| Nextorm_PreparedForLoop_ToListAsync |  49.484 ms |  1.01 |   727.2727 |  90.9091 |   6.23 MB |        1.00 |
| Nextorm_Cached_AsyncStream          |  57.752 ms |  1.18 |  3000.0000 |        - |  30.26 MB |        4.87 |
| Dapper_Async                        |  68.196 ms |  1.39 |  1000.0000 | 125.0000 |   8.87 MB |        1.43 |
| EFCore_Compiled_ToListAsync         | 102.706 ms |  2.10 |  1600.0000 | 200.0000 |  13.57 MB |        2.18 |
| Nextorm_Cached_ToList               | 111.154 ms |  2.27 |  3600.0000 | 200.0000 |  29.56 MB |        4.76 |
| Linq2Db_ToListAsync                 | 393.247 ms |  8.03 | 13000.0000 |        - | 107.83 MB |       17.35 |
