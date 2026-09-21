```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  Categories=InMemoryNew  

```
| Method                      | Mean      | Error      | StdDev    | Ratio | RatioSD | Gen0       | Gen1      | Gen2      | Allocated | Alloc Ratio |
|---------------------------- |----------:|-----------:|----------:|------:|--------:|-----------:|----------:|----------:|----------:|------------:|
| Linq_SelectMany             |  14.63 ms |   3.773 ms |  0.207 ms |  0.13 |    0.01 |  4781.2500 |         - |         - |  38.15 MB |        0.25 |
| EFCoreInMemory_GroupJoin    |  30.84 ms |  27.991 ms |  1.534 ms |  0.28 |    0.02 |  5812.5000 | 1937.5000 |         - |  46.72 MB |        0.31 |
| Linq_GroupJoin              |  35.86 ms |   2.513 ms |  0.138 ms |  0.33 |    0.01 |  5615.3846 | 1384.6154 |         - |  44.93 MB |        0.30 |
| Nextorm_SelectMany_Prepared |  57.53 ms | 117.380 ms |  6.434 ms |  0.53 |    0.06 |  8300.0000 | 4100.0000 | 4100.0000 |  70.88 MB |        0.47 |
| Nextorm_SelectMany          | 108.87 ms |  94.630 ms |  5.187 ms |  1.00 |    0.06 | 14000.0000 |  500.0000 |         - | 151.99 MB |        1.00 |
| Nextorm_GroupJoin_Prepared  | 122.78 ms |  53.311 ms |  2.922 ms |  1.13 |    0.05 | 16400.0000 | 8200.0000 | 8200.0000 |  98.77 MB |        0.65 |
| Nextorm_GroupJoin           | 450.57 ms | 742.922 ms | 40.722 ms |  4.14 |    0.37 | 20000.0000 | 9000.0000 | 3000.0000 | 215.65 MB |        1.42 |
