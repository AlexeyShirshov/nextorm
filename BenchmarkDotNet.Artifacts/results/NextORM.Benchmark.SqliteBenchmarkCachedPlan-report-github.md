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
| Cached_PlanOnly_Param |   535.2 μs |  40.93 μs |  2.24 μs |  0.51 | 59.5703 | 489.08 KB |        6.42 |
| Prepared_ToList       | 1,050.5 μs | 227.71 μs | 12.48 μs |  1.00 |  7.8125 |  76.14 KB |        1.00 |
| Cached_ToList         | 2,100.2 μs | 933.94 μs | 51.19 μs |  2.00 | 66.4063 | 565.23 KB |        7.42 |
