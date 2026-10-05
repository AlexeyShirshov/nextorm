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
| Cached_PlanOnly_Param |   539.8 μs |   168.0 μs |  9.21 μs |  0.50 | 56.6406 | 464.08 KB |        6.09 |
| Prepared_ToList       | 1,087.6 μs | 1,011.1 μs | 55.42 μs |  1.00 |  7.8125 |  76.14 KB |        1.00 |
| Cached_ToList         | 1,818.0 μs |   468.5 μs | 25.68 μs |  1.67 | 64.4531 | 540.22 KB |        7.09 |
