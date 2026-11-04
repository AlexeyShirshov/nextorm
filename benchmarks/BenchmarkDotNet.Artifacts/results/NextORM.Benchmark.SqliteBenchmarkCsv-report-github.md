```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  Categories=csv  

```
| Method                            | Mean      | Ratio | Gen0     | Gen1     | Gen2     | Allocated  | Alloc Ratio |
|---------------------------------- |----------:|------:|---------:|---------:|---------:|-----------:|------------:|
| Nextorm_StringProjection_ToList   |  2.819 ms |  0.33 | 121.0938 |  46.8750 |        - | 1019.52 KB |        0.45 |
| Nextorm_StringProjection_WriteCsv |  4.029 ms |  0.47 | 109.3750 |        - |        - |  949.67 KB |        0.42 |
| Nextorm_ToList                    |  8.558 ms |  1.00 | 265.6250 | 156.2500 |        - | 2267.38 KB |        1.00 |
| Nextorm_ToList_ManualCsv          |  9.927 ms |  1.16 | 500.0000 | 468.7500 |        - | 4141.67 KB |        1.83 |
| Nextorm_WriteCsv                  | 11.076 ms |  1.29 | 203.1250 |        - |        - | 1743.61 KB |        0.77 |
| Dapper_ToList_ManualCsv           | 13.291 ms |  1.55 | 671.8750 | 281.2500 |  93.7500 | 4865.94 KB |        2.15 |
| Linq2Db_ToList_ManualCsv          | 13.385 ms |  1.56 | 593.7500 | 296.8750 |  93.7500 | 4322.11 KB |        1.91 |
| EFCore_ToList_ManualCsv           | 14.364 ms |  1.68 | 890.6250 | 359.3750 | 109.3750 | 6516.97 KB |        2.87 |
