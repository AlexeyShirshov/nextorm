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
| Cached_PlanOnly_Param |   547.4 μs |  98.44 μs | 5.40 μs |  0.60 | 61.5234 | 507.05 KB |        6.66 |
| Prepared_ToList       |   906.1 μs |  42.51 μs | 2.33 μs |  1.00 |  8.7891 |  76.14 KB |        1.00 |
| Cached_ToList         | 1,885.6 μs | 105.62 μs | 5.79 μs |  2.08 | 70.3125 | 583.19 KB |        7.66 |
