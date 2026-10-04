```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host]   : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  ShortRun : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Runtime=.NET 10.0  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method                         | Toolchain              | Mean       | Ratio | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------------- |----------------------- |-----------:|------:|---------:|--------:|-----------:|------------:|
| Build_Distinct                 | Default                |   432.1 μs |  0.90 |  61.0352 | 60.5469 |  502.34 KB |        0.84 |
| Build_Case                     | Default                |   433.1 μs |  0.90 |  58.5938 | 54.6875 |  505.48 KB |        0.84 |
| Build_Baseline_SimpleSelect    | Default                |   483.5 μs |  1.00 |  70.3125 | 42.9688 |  600.01 KB |        1.00 |
| Build_StringFn_ToUpper         | Default                |   523.5 μs |  1.09 |  78.1250 | 50.7813 |  660.95 KB |        1.10 |
| Build_Udf                      | Default                |   542.5 μs |  1.12 |  78.1250 | 50.7813 |  661.73 KB |        1.10 |
| Build_StringFn_Contains        | Default                |   582.8 μs |  1.21 |  78.1250 | 50.7813 |  664.06 KB |        1.11 |
| Build_Except                   | Default                |   604.1 μs |  1.25 | 105.4688 | 62.5000 |  866.44 KB |        1.44 |
| Build_Unary_Not                | Default                |   634.6 μs |  1.32 |  78.1250 | 50.7813 |  650.01 KB |        1.08 |
| Build_Tvf                      | Default                |   635.5 μs |  1.32 |  66.4063 | 62.5000 |     550 KB |        0.92 |
| Build_In_ListContains_Captured | Default                |   883.4 μs |  1.83 |  93.7500 | 85.9375 |  791.41 KB |        1.32 |
| Build_Intersect                | Default                | 1,026.4 μs |  2.13 | 101.5625 | 54.6875 |     868 KB |        1.45 |
| Build_In_AtIn_Inline           | Default                | 1,040.6 μs |  2.16 |  93.7500 | 85.9375 |  818.75 KB |        1.36 |
| Build_LeftJoin                 | Default                | 1,069.8 μs |  2.22 | 109.3750 | 70.3125 |   927.4 KB |        1.55 |
| Build_In_ListContains_Inline   | Default                | 1,071.6 μs |  2.22 |  93.7500 | 85.9375 |  814.84 KB |        1.36 |
| Build_In_AtIn_Captured         | Default                | 1,098.6 μs |  2.28 |  93.7500 | 85.9375 |  784.38 KB |        1.31 |
| Build_Window_SumOver           | Default                | 1,169.9 μs |  2.43 |  85.9375 | 54.6875 |  717.23 KB |        1.20 |
| Build_Window_RowNumber         | Default                | 1,181.7 μs |  2.45 |  85.9375 | 78.1250 |  728.19 KB |        1.21 |
| Build_Join4                    | Default                | 1,958.5 μs |  4.06 | 220.7031 | 39.0625 | 1816.59 KB |        3.03 |
| Build_Cte                      | Default                | 2,194.3 μs |  4.55 | 218.7500 | 93.7500 | 1903.67 KB |        3.17 |
| Build_RecursiveCte             | Default                | 2,533.7 μs |  5.25 | 218.7500 | 93.7500 | 1874.26 KB |        3.12 |
|                                |                        |            |       |          |         |            |             |
| Build_Distinct                 | InProcessEmitToolchain |   477.1 μs |  0.92 |  61.0352 | 60.5469 |  502.35 KB |        0.83 |
| Build_Case                     | InProcessEmitToolchain |   508.9 μs |  0.98 |  61.5234 | 61.0352 |  505.48 KB |        0.83 |
| Build_Baseline_SimpleSelect    | InProcessEmitToolchain |   518.2 μs |  1.00 |  74.2188 | 49.3164 |  608.22 KB |        1.00 |
| Build_Unary_Not                | InProcessEmitToolchain |   629.4 μs |  1.22 |  83.0078 | 33.2031 |  678.73 KB |        1.12 |
| Build_Tvf                      | InProcessEmitToolchain |   648.3 μs |  1.25 |  65.4297 | 64.4531 |  539.07 KB |        0.89 |
| Build_Udf                      | InProcessEmitToolchain |   678.3 μs |  1.31 |  83.9844 | 55.6641 |  688.17 KB |        1.13 |
| Build_Intersect                | InProcessEmitToolchain |   696.9 μs |  1.35 | 109.3750 | 42.9688 |  899.07 KB |        1.48 |
| Build_StringFn_ToUpper         | InProcessEmitToolchain |   697.9 μs |  1.35 |  83.9844 | 55.6641 |  689.67 KB |        1.13 |
| Build_Except                   | InProcessEmitToolchain |   732.5 μs |  1.41 | 109.3750 | 41.9922 |  898.29 KB |        1.48 |
| Build_StringFn_Contains        | InProcessEmitToolchain |   763.9 μs |  1.48 |  83.9844 | 55.6641 |  692.78 KB |        1.14 |
| Build_In_AtIn_Captured         | InProcessEmitToolchain |   864.8 μs |  1.67 |  98.6328 | 97.6563 |  813.09 KB |        1.34 |
| Build_In_ListContains_Captured | InProcessEmitToolchain |   956.8 μs |  1.85 |  99.6094 | 98.6328 |  820.12 KB |        1.35 |
| Build_In_AtIn_Inline           | InProcessEmitToolchain | 1,003.1 μs |  1.94 | 101.5625 | 99.6094 |  836.54 KB |        1.38 |
| Build_Window_RowNumber         | InProcessEmitToolchain | 1,021.1 μs |  1.97 |  88.8672 | 87.8906 |  731.29 KB |        1.20 |
| Build_In_ListContains_Inline   | InProcessEmitToolchain | 1,030.5 μs |  1.99 | 101.5625 | 99.6094 |  843.55 KB |        1.39 |
| Build_LeftJoin                 | InProcessEmitToolchain | 1,185.8 μs |  2.29 | 113.2813 | 74.2188 |  930.54 KB |        1.53 |
| Build_Window_SumOver           | InProcessEmitToolchain | 1,220.6 μs |  2.36 |  87.8906 | 56.6406 |  720.35 KB |        1.18 |
| Build_RecursiveCte             | InProcessEmitToolchain | 1,836.8 μs |  3.55 | 234.3750 | 23.4375 | 1915.68 KB |        3.15 |
| Build_Join4                    | InProcessEmitToolchain | 2,418.3 μs |  4.67 | 218.7500 | 46.8750 | 1808.02 KB |        2.97 |
| Build_Cte                      | InProcessEmitToolchain | 2,704.7 μs |  5.22 | 230.4688 | 35.1563 | 1898.62 KB |        3.12 |
