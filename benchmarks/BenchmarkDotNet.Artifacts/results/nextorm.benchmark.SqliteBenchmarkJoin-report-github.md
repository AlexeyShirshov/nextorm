```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method           | Mean     | Error     | StdDev   | Gen0    | Gen1   | Allocated |
|----------------- |---------:|----------:|---------:|--------:|-------:|----------:|
| Nextorm_Prepared | 109.5 μs |  14.30 μs |  0.78 μs |  1.4648 |      - |  12.31 KB |
| Dapper           | 201.7 μs |  19.62 μs |  1.08 μs |  2.4414 |      - |  21.46 KB |
| Nextorm_Cached   | 348.0 μs |  80.11 μs |  4.39 μs | 11.2305 |      - |  92.18 KB |
| EFCore_Compiled  | 547.7 μs | 394.66 μs | 21.63 μs | 14.6484 | 4.8828 | 124.15 KB |
| Linq2Db          | 627.4 μs |  52.80 μs |  2.89 μs | 12.6953 |      - | 106.14 KB |
| EFCore           | 929.0 μs | 133.66 μs |  7.33 μs | 21.4844 | 6.8359 | 182.12 KB |
