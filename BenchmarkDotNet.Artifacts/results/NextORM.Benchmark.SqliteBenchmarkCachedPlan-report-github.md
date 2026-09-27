```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Runtime=.NET 10.0  Toolchain=InProcessEmitToolchain  
IterationCount=3  LaunchCount=1  WarmupCount=3  
Categories=acceptance  

```
| Method                | Mean       | Error      | StdDev    | Ratio | Gen0    | Allocated | Alloc Ratio |
|---------------------- |-----------:|-----------:|----------:|------:|--------:|----------:|------------:|
| Cached_PlanOnly_Param |   501.4 μs |   297.5 μs |  16.31 μs |  0.34 | 59.5703 | 489.86 KB |        6.43 |
| Prepared_ToList       | 1,547.1 μs | 6,745.7 μs | 369.76 μs |  1.05 |  7.8125 |  76.14 KB |        1.00 |
| Cached_ToList         | 1,835.3 μs | 4,735.9 μs | 259.59 μs |  1.24 | 68.3594 |    566 KB |        7.43 |
