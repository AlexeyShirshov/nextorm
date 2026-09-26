```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Runtime=.NET 10.0  Toolchain=InProcessEmitToolchain  
IterationCount=3  LaunchCount=1  WarmupCount=3  
Categories=acceptance  

```
| Method                | Mean       | Error     | StdDev   | Ratio | Gen0    | Allocated | Alloc Ratio |
|---------------------- |-----------:|----------:|---------:|------:|--------:|----------:|------------:|
| Cached_PlanOnly_Param |   500.4 μs |  33.77 μs |  1.85 μs |  0.56 | 61.5234 | 503.14 KB |        6.61 |
| Prepared_ToList       |   892.3 μs |  99.18 μs |  5.44 μs |  1.00 |  8.7891 |  76.14 KB |        1.00 |
| Cached_ToList         | 1,698.7 μs | 381.54 μs | 20.91 μs |  1.90 | 70.3125 | 579.28 KB |        7.61 |
