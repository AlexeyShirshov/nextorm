```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Runtime=.NET 10.0  Toolchain=InProcessEmitToolchain  
IterationCount=3  LaunchCount=1  WarmupCount=3  

```
| Method                                 | Mean     | Ratio | Gen0    | Gen1   | Allocated | Alloc Ratio |
|--------------------------------------- |---------:|------:|--------:|-------:|----------:|------------:|
| Warm_PlanOnly_Distinct                 | 229.7 μs |  1.00 | 41.5039 | 0.2441 | 340.63 KB |        1.00 |
| Warm_PlanOnly_In_AtIn_Inline           | 652.9 μs |  2.84 | 69.3359 |      - | 569.54 KB |        1.67 |
| Warm_PlanOnly_In_AtIn_Captured         | 707.8 μs |  3.08 | 82.0313 |      - | 677.36 KB |        1.99 |
| Warm_PlanOnly_In_ListContains_Inline   | 708.4 μs |  3.08 | 70.3125 |      - | 578.13 KB |        1.70 |
| Warm_PlanOnly_In_ListContains_Captured | 802.0 μs |  3.49 | 83.9844 |      - | 689.86 KB |        2.03 |
