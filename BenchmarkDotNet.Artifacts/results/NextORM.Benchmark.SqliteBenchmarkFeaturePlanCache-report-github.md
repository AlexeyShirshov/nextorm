```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Runtime=.NET 10.0  Toolchain=InProcessEmitToolchain  
IterationCount=3  LaunchCount=1  WarmupCount=3  

```
| Method                                 | Mean     | Ratio | Gen0    | Gen1   | Allocated | Alloc Ratio |
|--------------------------------------- |---------:|------:|--------:|-------:|----------:|------------:|
| Warm_PlanOnly_Distinct                 | 360.0 μs |  1.00 | 54.6875 |      - | 448.44 KB |        1.00 |
| Warm_PlanOnly_In_AtIn_Inline           | 881.6 μs |  2.46 | 83.9844 | 0.9766 | 692.19 KB |        1.54 |
| Warm_PlanOnly_In_AtIn_Captured         | 915.7 μs |  2.55 | 99.6094 |      - | 817.19 KB |        1.82 |
| Warm_PlanOnly_In_ListContains_Inline   | 920.6 μs |  2.57 | 83.9844 | 0.9766 | 690.63 KB |        1.54 |
| Warm_PlanOnly_In_ListContains_Captured | 977.9 μs |  2.73 | 99.6094 |      - | 821.88 KB |        1.83 |
