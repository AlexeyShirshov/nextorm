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
| Cached_PlanOnly_Param |   775.1 μs | 819.9 μs | 44.94 μs |  0.68 | 62.5000 |  513.3 KB |        6.74 |
| Prepared_ToList       | 1,136.3 μs | 913.6 μs | 50.08 μs |  1.00 |  7.8125 |  76.14 KB |        1.00 |
| Cached_ToList         | 2,485.0 μs | 880.6 μs | 48.27 μs |  2.19 | 70.3125 | 589.45 KB |        7.74 |
