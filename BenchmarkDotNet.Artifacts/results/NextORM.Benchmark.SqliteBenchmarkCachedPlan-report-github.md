```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Runtime=.NET 10.0  Toolchain=InProcessEmitToolchain  
IterationCount=3  LaunchCount=1  WarmupCount=3  
Categories=acceptance  

```
| Method                | Mean       | Error      | StdDev   | Ratio | Gen0    | Allocated | Alloc Ratio |
|---------------------- |-----------:|-----------:|---------:|------:|--------:|----------:|------------:|
| Cached_PlanOnly_Param |   581.6 μs | 1,410.3 μs | 77.30 μs |  0.30 | 56.6406 | 464.85 KB |        5.75 |
| Cached_ToList         | 1,769.7 μs |   353.0 μs | 19.35 μs |  0.90 | 62.5000 | 540.99 KB |        6.69 |
| Prepared_ToList       | 1,957.8 μs | 1,288.1 μs | 70.61 μs |  1.00 |  7.8125 |  80.82 KB |        1.00 |
