```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  Categories=csv  

```
| Method                            | Mean      | Ratio | Gen0     | Gen1     | Gen2    | Allocated  | Alloc Ratio |
|---------------------------------- |----------:|------:|---------:|---------:|--------:|-----------:|------------:|
| Nextorm_StringProjection_ToList   |  2.799 ms |  0.34 | 121.0938 |  54.6875 |       - | 1019.05 KB |        0.45 |
| Nextorm_StringProjection_WriteCsv |  3.423 ms |  0.41 | 113.2813 |        - |       - |  948.99 KB |        0.42 |
| Nextorm_ToList                    |  8.278 ms |  1.00 | 265.6250 | 156.2500 |       - | 2267.52 KB |        1.00 |
| Nextorm_ToList_ManualCsv          |  9.806 ms |  1.18 | 500.0000 | 468.7500 |       - | 4141.82 KB |        1.83 |
| Nextorm_WriteCsv                  | 10.241 ms |  1.24 | 203.1250 |        - |       - | 1733.64 KB |        0.76 |
| Dapper_ToList_ManualCsv           | 14.115 ms |  1.71 | 671.8750 | 281.2500 | 93.7500 | 4866.08 KB |        2.15 |
| Linq2Db_ToList_ManualCsv          | 14.635 ms |  1.77 | 593.7500 | 296.8750 | 93.7500 | 4321.92 KB |        1.91 |
| EFCore_ToList_ManualCsv           | 16.190 ms |  1.96 | 875.0000 | 343.7500 | 93.7500 | 6516.91 KB |        2.87 |
