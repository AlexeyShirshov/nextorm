```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Runtime=.NET 10.0  Toolchain=InProcessEmitToolchain  
IterationCount=3  LaunchCount=1  WarmupCount=3  

```
| Method                                 | Mean     | Ratio | Gen0    | Allocated | Alloc Ratio |
|--------------------------------------- |---------:|------:|--------:|----------:|------------:|
| Warm_PlanOnly_Distinct                 | 166.0 μs |  1.00 | 24.9023 | 204.69 KB |        1.00 |
| Warm_PlanOnly_In_AtIn_Captured         | 668.0 μs |  4.02 | 65.4297 |  542.2 KB |        2.65 |
| Warm_PlanOnly_In_ListContains_Captured | 731.2 μs |  4.41 | 61.5234 | 510.17 KB |        2.49 |
| Warm_PlanOnly_In_AtIn_Inline           | 781.9 μs |  4.71 | 68.3594 | 565.64 KB |        2.76 |
| Warm_PlanOnly_In_ListContains_Inline   | 870.8 μs |  5.25 | 64.4531 | 533.61 KB |        2.61 |
