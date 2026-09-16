```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Runtime=.NET 10.0  Toolchain=InProcessEmitToolchain  
IterationCount=3  LaunchCount=1  WarmupCount=3  

```
| Method                   | Mean          | Ratio | Gen0     | Gen1     | Allocated  | Alloc Ratio |
|------------------------- |--------------:|------:|---------:|---------:|-----------:|------------:|
| RePrepare_PlanOnly_Param |      94.83 μs | 0.001 |   4.7607 |        - |   39.84 KB |        1.18 |
| Construct_Only           |     115.24 μs | 0.001 |  20.5078 |        - |  167.97 KB |        4.97 |
| Cached_PlanOnly_NoParam  |     318.22 μs | 0.003 |  32.7148 |        - |  268.75 KB |        7.95 |
| Cached_PlanOnly_Param    |     390.98 μs | 0.004 |  37.1094 |        - |  303.13 KB |        8.97 |
| Build_Sql                |   7,775.17 μs | 0.076 | 109.3750 | 101.5625 |  948.35 KB |       28.07 |
| Build_Sql_Join           |  27,818.45 μs | 0.271 | 218.7500 | 187.5000 | 1964.88 KB |       58.15 |
| Prepared_ToList          | 102,943.03 μs | 1.002 |        - |        - |   33.79 KB |        1.00 |
| Cached_ToList            | 106,025.61 μs | 1.032 |        - |        - |  338.22 KB |       10.01 |
