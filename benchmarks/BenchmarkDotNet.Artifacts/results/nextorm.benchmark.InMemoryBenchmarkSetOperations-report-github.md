```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  Categories=InMemoryNew  

```
| Method                   | Mean      | Error      | StdDev    | Ratio | RatioSD | Gen0       | Gen1       | Gen2      | Allocated | Alloc Ratio |
|------------------------- |----------:|-----------:|----------:|------:|--------:|-----------:|-----------:|----------:|----------:|------------:|
| Linq_Intersect           |  11.00 ms |   0.608 ms |  0.033 ms |  0.29 |    0.00 |  1109.3750 |   218.7500 |         - |   8.94 MB |        0.15 |
| Linq_Except              |  15.60 ms |   3.063 ms |  0.168 ms |  0.41 |    0.01 |  3218.7500 |  1937.5000 | 1531.2500 |  27.49 MB |        0.45 |
| Linq_Union               |  24.88 ms |   1.392 ms |  0.076 ms |  0.65 |    0.01 |  5250.0000 |  2812.5000 | 2562.5000 |   51.4 MB |        0.84 |
| Nextorm_Intersect        |  35.04 ms |   5.860 ms |  0.321 ms |  0.91 |    0.01 |  7692.3077 |  3307.6923 | 1307.6923 |   61.1 MB |        1.00 |
| Nextorm_Except           |  38.42 ms |  11.489 ms |  0.630 ms |  1.00 |    0.02 |  7857.1429 |  3428.5714 | 1428.5714 |   61.1 MB |        1.00 |
| Nextorm_Union            |  49.37 ms |  10.326 ms |  0.566 ms |  1.29 |    0.02 | 10400.0000 |  5500.0000 | 3300.0000 |  87.09 MB |        1.43 |
| EFCoreInMemory_Intersect | 226.18 ms |  39.049 ms |  2.140 ms |  5.89 |    0.10 | 45000.0000 | 33000.0000 | 1333.3333 | 362.53 MB |        5.93 |
| EFCoreInMemory_Except    | 558.31 ms | 139.017 ms |  7.620 ms | 14.53 |    0.27 | 50000.0000 | 17000.0000 | 6000.0000 | 396.65 MB |        6.49 |
| EFCoreInMemory_Union     | 638.48 ms | 616.642 ms | 33.800 ms | 16.62 |    0.80 | 65000.0000 | 23000.0000 | 5000.0000 | 521.05 MB |        8.53 |
