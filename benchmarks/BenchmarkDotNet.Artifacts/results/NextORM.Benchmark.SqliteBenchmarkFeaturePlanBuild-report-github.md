```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host]   : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  ShortRun : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Runtime=.NET 10.0  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method                         | Toolchain              | Mean       | Ratio | Gen0     | Gen1     | Allocated  | Alloc Ratio |
|------------------------------- |----------------------- |-----------:|------:|---------:|---------:|-----------:|------------:|
| Build_Distinct                 | Default                |   384.8 μs |  0.81 |  58.5938 |  58.1055 |  481.25 KB |        0.82 |
| Build_Case                     | Default                |   428.7 μs |  0.90 |  58.5938 |  54.6875 |  490.63 KB |        0.84 |
| Build_Baseline_SimpleSelect    | Default                |   476.0 μs |  1.00 |  70.3125 |  42.9688 |  585.17 KB |        1.00 |
| Build_Tvf                      | Default                |   530.1 μs |  1.11 |  62.5000 |  58.5938 |  528.13 KB |        0.90 |
| Build_Intersect                | Default                |   795.9 μs |  1.67 |  93.7500 |  54.6875 |  819.56 KB |        1.40 |
| Build_Except                   | Default                |   882.3 μs |  1.85 |  93.7500 |  54.6875 |  818.78 KB |        1.40 |
| Build_Udf                      | Default                |   889.0 μs |  1.87 |  78.1250 |  46.8750 |  687.61 KB |        1.18 |
| Build_StringFn_ToUpper         | Default                |   938.1 μs |  1.97 |  78.1250 |  46.8750 |  686.82 KB |        1.17 |
| Build_Window_RowNumber         | Default                |   944.9 μs |  1.99 |  85.9375 |  78.1250 |  728.97 KB |        1.25 |
| Build_Unary_Not                | Default                | 1,014.0 μs |  2.13 |  78.1250 |  23.4375 |  675.88 KB |        1.16 |
| Build_StringFn_Contains        | Default                | 1,041.9 μs |  2.19 |  78.1250 |  46.8750 |  689.94 KB |        1.18 |
| Build_In_AtIn_Captured         | Default                | 1,082.6 μs |  2.28 |  93.7500 |  85.9375 |  822.77 KB |        1.41 |
| Build_In_ListContains_Captured | Default                | 1,175.8 μs |  2.47 | 101.5625 |  93.7500 |  836.06 KB |        1.43 |
| Build_In_AtIn_Inline           | Default                | 1,203.3 μs |  2.53 | 101.5625 |  93.7500 |  846.21 KB |        1.45 |
| Build_Window_SumOver           | Default                | 1,223.2 μs |  2.57 |  85.9375 |  54.6875 |  713.35 KB |        1.22 |
| Build_In_ListContains_Inline   | Default                | 1,327.0 μs |  2.79 | 101.5625 |  93.7500 |  870.42 KB |        1.49 |
| Build_Join4                    | Default                | 1,936.6 μs |  4.07 | 218.7500 |  50.7813 | 1796.28 KB |        3.07 |
| Build_Cte                      | Default                | 2,145.6 μs |  4.51 | 218.7500 |  35.1563 | 1791.55 KB |        3.06 |
| Build_RecursiveCte             | Default                | 2,281.5 μs |  4.80 | 203.1250 |  93.7500 | 1747.69 KB |        2.99 |
| Build_LeftJoin                 | Default                | 2,764.2 μs |  5.81 | 109.3750 |  62.5000 |  912.56 KB |        1.56 |
|                                |                        |            |       |          |          |            |             |
| Build_Distinct                 | InProcessEmitToolchain |   409.8 μs |  0.79 |  57.1289 |  56.6406 |  470.32 KB |        0.79 |
| Build_Case                     | InProcessEmitToolchain |   502.3 μs |  0.97 |  60.0586 |  59.5703 |  490.64 KB |        0.83 |
| Build_Baseline_SimpleSelect    | InProcessEmitToolchain |   520.0 μs |  1.00 |  72.2656 |  47.8516 |  593.38 KB |        1.00 |
| Build_StringFn_ToUpper         | InProcessEmitToolchain |   604.5 μs |  1.16 |  82.0313 |  53.7109 |  675.61 KB |        1.14 |
| Build_StringFn_Contains        | InProcessEmitToolchain |   611.5 μs |  1.18 |  83.0078 |  54.6875 |  678.72 KB |        1.14 |
| Build_Tvf                      | InProcessEmitToolchain |   613.2 μs |  1.18 |  64.4531 |  63.4766 |  528.13 KB |        0.89 |
| Build_Udf                      | InProcessEmitToolchain |   615.8 μs |  1.18 |  82.0313 |  53.7109 |  674.24 KB |        1.14 |
| Build_Unary_Not                | InProcessEmitToolchain |   634.0 μs |  1.22 |  81.0547 |  36.1328 |  664.67 KB |        1.12 |
| Build_Intersect                | InProcessEmitToolchain |   702.2 μs |  1.35 | 103.5156 |  61.5234 |   851.8 KB |        1.44 |
| Build_Except                   | InProcessEmitToolchain |   703.7 μs |  1.35 | 103.5156 |  61.5234 |   851.8 KB |        1.44 |
| Build_In_AtIn_Captured         | InProcessEmitToolchain |   828.5 μs |  1.59 |  98.6328 |  97.6563 |  811.53 KB |        1.37 |
| Build_In_ListContains_Captured | InProcessEmitToolchain |   933.6 μs |  1.80 | 100.5859 |  99.6094 |  824.81 KB |        1.39 |
| Build_In_AtIn_Inline           | InProcessEmitToolchain |   989.8 μs |  1.90 | 101.5625 |  99.6094 |  834.97 KB |        1.41 |
| Build_Window_RowNumber         | InProcessEmitToolchain | 1,084.2 μs |  2.09 |  87.8906 |  85.9375 |  732.08 KB |        1.23 |
| Build_In_ListContains_Inline   | InProcessEmitToolchain | 1,112.0 μs |  2.14 | 103.5156 | 101.5625 |  848.24 KB |        1.43 |
| Build_Window_SumOver           | InProcessEmitToolchain | 1,115.7 μs |  2.15 |  85.9375 |  56.6406 |  708.64 KB |        1.19 |
| Build_LeftJoin                 | InProcessEmitToolchain | 1,148.3 μs |  2.21 | 111.3281 |  72.2656 |  915.69 KB |        1.54 |
| Build_RecursiveCte             | InProcessEmitToolchain | 1,951.6 μs |  3.75 | 218.7500 |  31.2500 | 1792.25 KB |        3.02 |
| Build_Join4                    | InProcessEmitToolchain | 2,385.1 μs |  4.59 | 222.6563 |  70.3125 | 1818.96 KB |        3.07 |
| Build_Cte                      | InProcessEmitToolchain | 2,637.1 μs |  5.07 | 218.7500 |  35.1563 | 1814.08 KB |        3.06 |
