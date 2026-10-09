```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Runtime=.NET 10.0  Toolchain=InProcessEmitToolchain  
IterationCount=3  LaunchCount=1  WarmupCount=3  
Categories=acceptance  

```
| Method                | Mean       | Error     | StdDev  | Ratio | Gen0    | Allocated | Alloc Ratio |
|---------------------- |-----------:|----------:|--------:|------:|--------:|----------:|------------:|
| Cached_PlanOnly_Param |   557.9 μs |  99.33 μs | 5.44 μs |  0.61 | 61.5234 | 507.83 KB |        6.67 |
| Prepared_ToList       |   909.5 μs |  95.84 μs | 5.25 μs |  1.00 |  8.7891 |  76.14 KB |        1.00 |
| Cached_ToList         | 1,834.5 μs | 118.57 μs | 6.50 μs |  2.02 | 70.3125 | 583.97 KB |        7.67 |
