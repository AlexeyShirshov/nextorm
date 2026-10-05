```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Runtime=.NET 10.0  Toolchain=InProcessEmitToolchain  
IterationCount=3  LaunchCount=1  WarmupCount=3  
Categories=acceptance  

```
| Method                | Mean       | Error       | StdDev    | Ratio | Gen0    | Allocated | Alloc Ratio |
|---------------------- |-----------:|------------:|----------:|------:|--------:|----------:|------------:|
| Cached_PlanOnly_Param |   537.0 μs |    24.80 μs |   1.36 μs |  0.39 | 58.5938 | 478.92 KB |        6.25 |
| Prepared_ToList       | 1,452.0 μs | 7,054.67 μs | 386.69 μs |  1.05 |  7.8125 |  76.69 KB |        1.00 |
| Cached_ToList         | 1,812.1 μs |   152.10 μs |   8.34 μs |  1.31 | 66.4063 | 555.05 KB |        7.24 |
