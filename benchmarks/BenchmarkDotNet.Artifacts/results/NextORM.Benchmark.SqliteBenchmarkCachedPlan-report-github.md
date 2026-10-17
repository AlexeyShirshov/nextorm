```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host]   : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  ShortRun : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Runtime=.NET 10.0  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method                     | Toolchain              | Categories | Mean       | Error       | StdDev    | Ratio | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|--------------------------- |----------------------- |----------- |-----------:|------------:|----------:|------:|---------:|--------:|-----------:|------------:|
| Construct_Only             | Default                |            |   116.1 μs |   100.17 μs |   5.49 μs |  0.13 |  30.7617 |       - |  251.57 KB |        3.30 |
| RePrepare_PlanOnly_Param   | Default                |            |   261.9 μs |    47.73 μs |   2.62 μs |  0.30 |  17.5781 |       - |  144.53 KB |        1.90 |
| Cached_PlanOnly_NoParam    | Default                |            |   292.9 μs |    55.19 μs |   3.03 μs |  0.33 |  39.5508 |       - |  323.44 KB |        4.25 |
| Cached_PlanOnly_Param      | Default                | acceptance |   367.5 μs |    90.44 μs |   4.96 μs |  0.42 |  42.9688 |       - |  357.82 KB |        4.70 |
| Prepared_ToList            | Default                | acceptance |   878.8 μs |   169.56 μs |   9.29 μs |  1.00 |   7.8125 |       - |   76.13 KB |        1.00 |
| M12_NoCache_PlanOnly_Param | Default                |            |   885.3 μs | 4,002.32 μs | 219.38 μs |  1.01 |  78.1250 | 70.3125 |  689.86 KB |        9.06 |
| Build_Sql                  | Default                |            |   983.8 μs | 5,227.94 μs | 286.56 μs |  1.12 |  78.1250 | 70.3125 |  656.26 KB |        8.62 |
| Cached_ToList              | Default                | acceptance | 1,426.6 μs |   370.27 μs |  20.30 μs |  1.62 |  46.8750 |       - |  433.95 KB |        5.70 |
| Build_Sql_Join             | Default                |            | 1,601.2 μs |   273.49 μs |  14.99 μs |  1.82 | 162.1094 | 89.8438 | 1339.92 KB |       17.60 |
| M12_NoCache_ToList         | Default                |            | 3,160.8 μs |   340.21 μs |  18.65 μs |  3.60 |  93.7500 | 78.1250 |  769.16 KB |       10.10 |
|                            |                        |            |            |             |           |       |          |         |            |             |
| Construct_Only             | InProcessEmitToolchain |            |   111.6 μs |    38.52 μs |   2.11 μs |  0.13 |  30.7617 |       - |  251.57 KB |        3.30 |
| RePrepare_PlanOnly_Param   | InProcessEmitToolchain |            |   275.9 μs |    17.03 μs |   0.93 μs |  0.31 |  17.5781 |       - |  144.53 KB |        1.90 |
| Cached_PlanOnly_NoParam    | InProcessEmitToolchain |            |   289.6 μs |    16.12 μs |   0.88 μs |  0.33 |  39.5508 |       - |  323.44 KB |        4.25 |
| Cached_PlanOnly_Param      | InProcessEmitToolchain | acceptance |   342.3 μs |    41.81 μs |   2.29 μs |  0.38 |  43.4570 |       - |  357.82 KB |        4.70 |
| M12_NoCache_PlanOnly_Param | InProcessEmitToolchain |            |   591.2 μs |   132.54 μs |   7.26 μs |  0.66 |  83.0078 | 82.0313 |     684 KB |        8.98 |
| Build_Sql                  | InProcessEmitToolchain |            |   600.1 μs |    38.04 μs |   2.08 μs |  0.67 |  80.0781 | 79.1016 |  656.27 KB |        8.62 |
| Prepared_ToList            | InProcessEmitToolchain | acceptance |   890.0 μs |   107.62 μs |   5.90 μs |  1.00 |   8.7891 |       - |   76.14 KB |        1.00 |
| Cached_ToList              | InProcessEmitToolchain | acceptance | 1,416.9 μs |    85.29 μs |   4.68 μs |  1.59 |  52.7344 |       - |  433.96 KB |        5.70 |
| Build_Sql_Join             | InProcessEmitToolchain |            | 1,884.9 μs |   238.86 μs |  13.09 μs |  2.12 | 164.0625 | 89.8438 |    1354 KB |       17.78 |
| M12_NoCache_ToList         | InProcessEmitToolchain |            | 3,067.6 μs |   682.51 μs |  37.41 μs |  3.45 |  93.7500 | 89.8438 |  777.38 KB |       10.21 |
