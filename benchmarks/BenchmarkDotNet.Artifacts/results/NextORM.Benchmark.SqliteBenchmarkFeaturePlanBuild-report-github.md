```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Runtime=.NET 10.0  Toolchain=InProcessEmitToolchain  
IterationCount=3  LaunchCount=1  WarmupCount=3  

```
| Method                         | Mean       | Ratio | Gen0     | Gen1     | Allocated  | Alloc Ratio |
|------------------------------- |-----------:|------:|---------:|---------:|-----------:|------------:|
| Build_Distinct                 |   323.1 μs |  0.87 |  50.7813 |  50.2930 |  417.97 KB |        0.80 |
| Build_Case                     |   362.9 μs |  0.98 |  53.2227 |  52.7344 |  438.29 KB |        0.84 |
| Build_Baseline_SimpleSelect    |   371.0 μs |  1.00 |  62.5000 |  60.5469 |  524.23 KB |        1.00 |
| Build_StringFn_Contains        |   464.2 μs |  1.25 |  74.2188 |  73.7305 |  608.99 KB |        1.16 |
| Build_StringFn_ToUpper         |   477.9 μs |  1.29 |  73.7305 |  73.2422 |  605.87 KB |        1.16 |
| Build_Tvf                      |   500.3 μs |  1.35 |  54.6875 |  53.7109 |  452.35 KB |        0.86 |
| Build_Except                   |   581.7 μs |  1.57 |  91.7969 |  56.6406 |  752.96 KB |        1.44 |
| Build_Intersect                |   611.8 μs |  1.65 |  91.7969 |  60.5469 |  753.75 KB |        1.44 |
| Build_Unary_Not                |   662.7 μs |  1.79 |  71.2891 |  46.8750 |  590.25 KB |        1.13 |
| Build_LeftJoin                 |   716.5 μs |  1.93 |  88.8672 |  87.8906 |  730.52 KB |        1.39 |
| Build_Udf                      |   731.1 μs |  1.97 |  77.1484 |  50.7813 |  634.26 KB |        1.21 |
| Build_In_ListContains_Captured |   884.7 μs |  2.38 |  91.7969 |  90.8203 |  752.76 KB |        1.44 |
| Build_In_AtIn_Inline           |   885.1 μs |  2.39 |  94.7266 |  93.7500 |  775.61 KB |        1.48 |
| Build_In_AtIn_Captured         |   914.1 μs |  2.46 |  91.7969 |  88.8672 |  752.17 KB |        1.43 |
| Build_Window_SumOver           | 1,025.4 μs |  2.76 |  78.1250 |  76.1719 |  653.96 KB |        1.25 |
| Build_Window_RowNumber         | 1,038.4 μs |  2.80 |  82.0313 |  80.0781 |  677.41 KB |        1.29 |
| Build_In_ListContains_Inline   | 1,152.0 μs |  3.11 |  94.7266 |  93.7500 |  776.19 KB |        1.48 |
| Build_RecursiveCte             | 1,378.4 μs |  3.72 | 187.5000 |  19.5313 | 1541.45 KB |        2.94 |
| Build_Join4                    | 1,405.6 μs |  3.79 | 175.7813 | 103.5156 | 1445.47 KB |        2.76 |
| Build_Cte                      | 1,729.8 μs |  4.66 | 185.5469 |  21.4844 | 1516.54 KB |        2.89 |
