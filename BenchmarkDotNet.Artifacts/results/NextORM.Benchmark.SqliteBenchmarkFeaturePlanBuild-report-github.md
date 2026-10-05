```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host]   : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  ShortRun : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Runtime=.NET 10.0  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method                         | Toolchain              | Mean        | Ratio | Gen0     | Gen1     | Allocated  | Alloc Ratio |
|------------------------------- |----------------------- |------------:|------:|---------:|---------:|-----------:|------------:|
| Build_Distinct                 | Default                |  2,375.8 μs |  0.57 |  62.5000 |  58.5938 |  529.69 KB |        0.84 |
| Build_Case                     | Default                |  2,463.0 μs |  0.59 |  74.2188 |  70.3125 |  612.51 KB |        0.98 |
| Build_Unary_Not                | Default                |  3,144.2 μs |  0.76 |  85.9375 |  19.5313 |   725.8 KB |        1.16 |
| Build_StringFn_ToUpper         | Default                |  3,163.2 μs |  0.76 |  85.9375 |  50.7813 |  721.11 KB |        1.15 |
| Build_StringFn_Contains        | Default                |  3,645.2 μs |  0.88 |  85.9375 |  50.7813 |     725 KB |        1.16 |
| Build_Baseline_SimpleSelect    | Default                |  4,171.6 μs |  1.00 |  74.2188 |  46.8750 |  627.36 KB |        1.00 |
| Build_Intersect                | Default                |  4,734.4 μs |  1.14 | 109.3750 |  31.2500 |  947.69 KB |        1.51 |
| Build_In_AtIn_Captured         | Default                |  4,781.7 μs |  1.15 | 101.5625 |  93.7500 |  838.28 KB |        1.34 |
| Build_Window_SumOver           | Default                |  4,993.7 μs |  1.20 |  85.9375 |  54.6875 |   747.7 KB |        1.19 |
| Build_Window_RowNumber         | Default                |  5,055.6 μs |  1.22 |  93.7500 |  54.6875 |  812.57 KB |        1.30 |
| Build_Udf                      | Default                |  5,092.3 μs |  1.22 |  85.9375 |  46.8750 |  715.64 KB |        1.14 |
| Build_Except                   | Default                |  5,308.2 μs |  1.28 | 109.3750 |  31.2500 |  946.91 KB |        1.51 |
| Build_In_ListContains_Captured | Default                |  5,327.5 μs |  1.28 | 101.5625 |  93.7500 |  845.31 KB |        1.35 |
| Build_In_AtIn_Inline           | Default                |  5,570.5 μs |  1.34 | 101.5625 |  93.7500 |  861.72 KB |        1.37 |
| Build_In_ListContains_Inline   | Default                |  5,826.6 μs |  1.40 | 101.5625 |  93.7500 |  868.75 KB |        1.38 |
| Build_Tvf                      | Default                |  6,056.0 μs |  1.46 |  70.3125 |  42.9688 |  603.91 KB |        0.96 |
| Build_LeftJoin                 | Default                |  9,359.6 μs |  2.25 | 109.3750 |  62.5000 |    1002 KB |        1.60 |
| Build_RecursiveCte             | Default                | 12,359.8 μs |  2.97 | 265.6250 | 109.3750 | 2185.58 KB |        3.48 |
| Build_Cte                      | Default                | 17,641.0 μs |  4.24 | 234.3750 |  93.7500 | 1995.07 KB |        3.18 |
| Build_Join4                    | Default                | 23,287.5 μs |  5.60 | 218.7500 |  93.7500 | 1948.64 KB |        3.11 |
|                                |                        |             |       |          |          |            |             |
| Build_Baseline_SimpleSelect    | InProcessEmitToolchain |    540.6 μs |  1.00 |  76.1719 |  48.8281 |  627.36 KB |        1.00 |
| Build_Distinct                 | InProcessEmitToolchain |    626.4 μs |  1.16 |  64.4531 |  63.4766 |  529.69 KB |        0.84 |
| Build_Tvf                      | InProcessEmitToolchain |    729.4 μs |  1.35 |  72.2656 |  47.8516 |  592.97 KB |        0.95 |
| Build_Unary_Not                | InProcessEmitToolchain |    732.2 μs |  1.36 |  85.9375 |  15.6250 |  703.92 KB |        1.12 |
| Build_Udf                      | InProcessEmitToolchain |    736.4 μs |  1.36 |  86.9141 |  51.7578 |  715.64 KB |        1.14 |
| Build_Case                     | InProcessEmitToolchain |    769.6 μs |  1.42 |  74.2188 |  73.2422 |  612.51 KB |        0.98 |
| Build_Intersect                | InProcessEmitToolchain |    803.8 μs |  1.49 | 115.2344 |  25.3906 |  947.69 KB |        1.51 |
| Build_Except                   | InProcessEmitToolchain |    898.9 μs |  1.66 | 115.2344 |  19.5313 |  946.91 KB |        1.51 |
| Build_In_AtIn_Inline           | InProcessEmitToolchain |  1,053.9 μs |  1.95 | 105.4688 | 103.5156 |  861.72 KB |        1.37 |
| Build_In_ListContains_Captured | InProcessEmitToolchain |  1,080.0 μs |  2.00 | 101.5625 |  99.6094 |  845.31 KB |        1.35 |
| Build_StringFn_ToUpper         | InProcessEmitToolchain |  1,124.0 μs |  2.08 |  85.9375 |  50.7813 |  714.86 KB |        1.14 |
| Build_In_ListContains_Inline   | InProcessEmitToolchain |  1,162.8 μs |  2.15 | 105.4688 | 103.5156 |  868.75 KB |        1.38 |
| Build_StringFn_Contains        | InProcessEmitToolchain |  1,218.8 μs |  2.26 |  87.8906 |  50.7813 |  717.97 KB |        1.14 |
| Build_LeftJoin                 | InProcessEmitToolchain |  1,391.4 μs |  2.58 | 117.1875 |  66.4063 |  979.75 KB |        1.56 |
| Build_In_AtIn_Captured         | InProcessEmitToolchain |  1,440.5 μs |  2.67 | 101.5625 |  99.6094 |  838.28 KB |        1.34 |
| Build_Window_SumOver           | InProcessEmitToolchain |  1,463.7 μs |  2.71 |  89.8438 |  58.5938 |  747.69 KB |        1.19 |
| Build_Window_RowNumber         | InProcessEmitToolchain |  1,885.5 μs |  3.49 |  97.6563 |  64.4531 |  812.55 KB |        1.30 |
| Build_RecursiveCte             | InProcessEmitToolchain |  2,414.0 μs |  4.47 | 261.7188 | 109.3750 | 2161.76 KB |        3.45 |
| Build_Cte                      | InProcessEmitToolchain |  2,843.6 μs |  5.26 | 242.1875 |  31.2500 | 1988.47 KB |        3.17 |
| Build_Join4                    | InProcessEmitToolchain |  3,070.7 μs |  5.68 | 234.3750 |  62.5000 | 1934.57 KB |        3.08 |
