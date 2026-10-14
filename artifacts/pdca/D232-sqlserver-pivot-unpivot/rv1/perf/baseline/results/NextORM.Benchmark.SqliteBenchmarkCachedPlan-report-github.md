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
| Cached_PlanOnly_Param |   539.4 μs | 214.1 μs | 11.74 μs |  0.60 | 62.5000 | 515.64 KB |        6.77 |
| Prepared_ToList       |   896.1 μs | 345.4 μs | 18.93 μs |  1.00 |  8.7891 |  76.14 KB |        1.00 |
| Cached_ToList         | 1,798.0 μs | 509.3 μs | 27.91 μs |  2.01 | 72.2656 | 591.78 KB |        7.77 |
