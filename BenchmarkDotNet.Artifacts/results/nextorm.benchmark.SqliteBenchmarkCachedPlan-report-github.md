```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.3 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Runtime=.NET 10.0  Toolchain=InProcessEmitToolchain  
IterationCount=3  LaunchCount=1  WarmupCount=3  

```
| Method                   | Mean         | Ratio | Gen0     | Gen1     | Allocated  | Alloc Ratio |
|------------------------- |-------------:|------:|---------:|---------:|-----------:|------------:|
| RePrepare_PlanOnly_Param |     79.73 μs |  0.09 |   4.7607 |        - |   39.84 KB |        1.18 |
| Construct_Only           |    110.46 μs |  0.13 |  20.5078 |        - |  167.97 KB |        4.97 |
| Cached_PlanOnly_NoParam  |    301.14 μs |  0.35 |  32.7148 |        - |  268.75 KB |        7.95 |
| Cached_PlanOnly_Param    |    379.02 μs |  0.44 |  37.1094 |        - |  303.13 KB |        8.97 |
| Prepared_ToList          |    864.17 μs |  1.00 |   3.9063 |        - |   33.79 KB |        1.00 |
| Cached_ToList            |  1,418.40 μs |  1.64 |  41.0156 |        - |  336.93 KB |        9.97 |
| Build_Sql                |  7,090.67 μs |  8.21 | 109.3750 | 101.5625 |  948.35 KB |       28.07 |
| Build_Sql_Join           | 25,251.38 μs | 29.22 | 218.7500 | 187.5000 | 1978.94 KB |       58.57 |
