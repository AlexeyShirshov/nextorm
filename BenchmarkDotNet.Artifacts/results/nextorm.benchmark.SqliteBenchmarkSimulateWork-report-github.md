```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.3 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Runtime=.NET 10.0  Toolchain=InProcessEmitToolchain  
IterationCount=3  LaunchCount=1  WarmupCount=3  

```
| Method                              | Mean       | Ratio | Gen0      | Gen1     | Allocated | Alloc Ratio |
|------------------------------------ |-----------:|------:|----------:|---------:|----------:|------------:|
| Nextorm_Prepared_AsyncStream        |   5.404 ms |  0.13 |  257.8125 |        - |   2.06 MB |        1.00 |
| Dapper_AsyncStream                  |  27.811 ms |  0.65 |  906.2500 |        - |   7.45 MB |        3.62 |
| Nextorm_Cached_AsyncStream          |  42.707 ms |  1.00 | 3333.3333 |        - |  26.98 MB |       13.09 |
| Nextorm_Prepared_ToListAsync        |  42.719 ms |  1.00 |  250.0000 |        - |   2.06 MB |        1.00 |
| Nextorm_PreparedForLoop_ToListAsync |  43.333 ms |  1.01 |  250.0000 |        - |   2.08 MB |        1.01 |
| Dapper_Async                        |  70.220 ms |  1.64 |  875.0000 | 125.0000 |   7.46 MB |        3.62 |
| Nextorm_Cached_ToList               |  88.305 ms |  2.07 | 3333.3333 | 166.6667 |  26.63 MB |       12.92 |
| EFCore_Compiled_ToListAsync         | 119.711 ms |  2.80 | 1600.0000 | 200.0000 |  13.89 MB |        6.74 |
