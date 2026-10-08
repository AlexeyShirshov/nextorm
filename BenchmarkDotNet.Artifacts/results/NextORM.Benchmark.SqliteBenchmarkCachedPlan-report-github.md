```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Runtime=.NET 10.0  Toolchain=InProcessEmitToolchain  
IterationCount=3  LaunchCount=1  WarmupCount=3  

```
| Method                     | Categories | Mean       | Error     | StdDev   | Ratio | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|--------------------------- |----------- |-----------:|----------:|---------:|------:|---------:|--------:|-----------:|------------:|
| Construct_Only             |            |   146.6 μs |  60.58 μs |  3.32 μs |  0.16 |  32.7148 |       - |  268.76 KB |        3.53 |
| RePrepare_PlanOnly_Param   |            |   293.8 μs | 195.74 μs | 10.73 μs |  0.32 |  22.4609 |       - |  184.38 KB |        2.42 |
| Cached_PlanOnly_NoParam    |            |   455.2 μs | 291.49 μs | 15.98 μs |  0.50 |  54.6875 |       - |  473.45 KB |        6.22 |
| Cached_PlanOnly_Param      | acceptance |   515.9 μs | 434.84 μs | 23.84 μs |  0.56 |  61.5234 |       - |  507.83 KB |        6.67 |
| M12_NoCache_PlanOnly_Param |            |   637.2 μs | 362.63 μs | 19.88 μs |  0.70 |  88.8672 | 87.8906 |  732.44 KB |        9.62 |
| Build_Sql                  |            |   653.8 μs | 308.82 μs | 16.93 μs |  0.72 |  85.9375 | 84.9609 |  704.71 KB |        9.26 |
| Prepared_ToList            | acceptance |   914.6 μs | 378.84 μs | 20.77 μs |  1.00 |   8.7891 |       - |   76.14 KB |        1.00 |
| Cached_ToList              | acceptance | 1,840.4 μs | 985.36 μs | 54.01 μs |  2.01 |  70.3125 |       - |  583.97 KB |        7.67 |
| Build_Sql_Join             |            | 2,038.4 μs | 510.38 μs | 27.98 μs |  2.23 | 171.8750 | 42.9688 | 1404.01 KB |       18.44 |
| M12_NoCache_ToList         |            | 3,229.1 μs | 465.56 μs | 25.52 μs |  3.53 |  97.6563 | 93.7500 |  825.82 KB |       10.85 |
