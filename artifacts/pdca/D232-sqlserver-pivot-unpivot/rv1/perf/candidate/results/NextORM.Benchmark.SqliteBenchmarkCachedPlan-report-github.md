```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Runtime=.NET 10.0  Toolchain=InProcessEmitToolchain  
IterationCount=3  LaunchCount=1  WarmupCount=3  
Categories=acceptance  

```
| Method                | Mean       | Error    | StdDev  | Ratio | Gen0    | Allocated | Alloc Ratio |
|---------------------- |-----------:|---------:|--------:|------:|--------:|----------:|------------:|
| Cached_PlanOnly_Param |   570.7 μs | 38.18 μs | 2.09 μs |  0.64 | 64.4531 | 526.58 KB |        6.92 |
| Prepared_ToList       |   885.6 μs | 91.77 μs | 5.03 μs |  1.00 |  8.7891 |  76.14 KB |        1.00 |
| Cached_ToList         | 1,880.8 μs | 86.57 μs | 4.75 μs |  2.12 | 72.2656 | 602.72 KB |        7.92 |
