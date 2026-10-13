```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Runtime=.NET 10.0  Toolchain=InProcessEmitToolchain  
IterationCount=3  LaunchCount=1  WarmupCount=3  
Categories=acceptance  

```
| Method                | Mean       | Error    | StdDev   | Ratio | Gen0    | Allocated | Alloc Ratio |
|---------------------- |-----------:|---------:|---------:|------:|--------:|----------:|------------:|
| Cached_PlanOnly_Param |   770.4 μs | 211.7 μs | 11.60 μs |  0.68 | 62.5000 |  513.3 KB |        6.74 |
| Prepared_ToList       | 1,134.9 μs | 632.0 μs | 34.64 μs |  1.00 |  7.8125 |  76.14 KB |        1.00 |
| Cached_ToList         | 2,576.9 μs | 222.0 μs | 12.17 μs |  2.27 | 70.3125 | 589.45 KB |        7.74 |
