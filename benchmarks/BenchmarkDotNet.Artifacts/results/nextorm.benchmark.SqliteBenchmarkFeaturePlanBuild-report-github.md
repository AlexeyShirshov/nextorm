```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Runtime=.NET 10.0  Toolchain=InProcessEmitToolchain  
IterationCount=3  LaunchCount=1  WarmupCount=3  

```
| Method                         | Mean       | Ratio | Gen0     | Gen1     | Allocated  | Alloc Ratio |
|------------------------------- |-----------:|------:|---------:|---------:|-----------:|------------:|
| Build_Distinct                 |   272.8 μs |  0.83 |  33.2031 |  32.7148 |  272.66 KB |        0.76 |
| Build_Baseline_SimpleSelect    |   330.4 μs |  1.00 |  42.9688 |  41.0156 |  359.38 KB |        1.00 |
| Build_Case                     |   344.1 μs |  1.04 |  40.0391 |  39.5508 |  327.35 KB |        0.91 |
| Build_StringFn_Contains        |   382.1 μs |  1.16 |  53.7109 |  53.2227 |  441.02 KB |        1.23 |
| Build_StringFn_ToUpper         |   402.5 μs |  1.22 |  53.2227 |  52.7344 |   437.9 KB |        1.22 |
| Build_Except                   |   440.0 μs |  1.33 |  65.4297 |  64.9414 |  537.33 KB |        1.50 |
| Build_Intersect                |   446.7 μs |  1.35 |  65.4297 |  64.9414 |  538.11 KB |        1.50 |
| Build_Tvf                      |   456.6 μs |  1.38 |  38.5742 |  38.0859 |  318.75 KB |        0.89 |
| Build_Unary_Not                |   458.9 μs |  1.39 |  51.7578 |  51.2695 |  424.62 KB |        1.18 |
| Build_Udf                      |   592.0 μs |  1.79 |  56.6406 |  55.6641 |  468.63 KB |        1.30 |
| Build_In_AtIn_Captured         |   671.0 μs |  2.03 |  75.1953 |  74.2188 |   617.6 KB |        1.72 |
| Build_LeftJoin                 |   671.7 μs |  2.03 |  76.1719 |  75.1953 |  624.26 KB |        1.74 |
| Build_In_ListContains_Captured |   751.0 μs |  2.27 |  71.2891 |  70.3125 |  584.78 KB |        1.63 |
| Build_In_AtIn_Inline           |   756.3 μs |  2.29 |  78.1250 |  77.1484 |  641.04 KB |        1.78 |
| Build_In_ListContains_Inline   |   774.6 μs |  2.34 |  74.2188 |  73.2422 |  608.22 KB |        1.69 |
| Build_Window_SumOver           |   887.4 μs |  2.69 |  69.3359 |  68.3594 |  569.57 KB |        1.58 |
| Build_Window_RowNumber         |   921.0 μs |  2.79 |  70.3125 |  69.3359 |  580.52 KB |        1.62 |
| Build_RecursiveCte             | 1,055.6 μs |  3.19 | 140.6250 |  50.7813 | 1157.85 KB |        3.22 |
| Build_Join4                    | 1,312.3 μs |  3.97 | 152.3438 | 150.3906 | 1248.57 KB |        3.47 |
| Build_Cte                      | 1,635.6 μs |  4.95 | 150.3906 |  54.6875 |  1233.7 KB |        3.43 |
