```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method                                    | Categories        | Mean       | Gen0    | Gen1   | Allocated |
|------------------------------------------ |------------------ |-----------:|--------:|-------:|----------:|
| B_Nextorm_Cached_CaseWhen                 | B_CaseWhen        |   144.9 μs |  5.3711 |      - |  44.69 KB |
| B_Dapper_CaseWhen                         | B_CaseWhen        |   150.4 μs |  1.4648 |      - |  13.44 KB |
| B_Linq2Db_CaseWhen                        | B_CaseWhen        |   252.7 μs |  4.3945 |      - |  38.05 KB |
| B_EFCore_CaseWhen                         | B_CaseWhen        |   598.0 μs | 13.6719 | 5.8594 | 119.54 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_Cte                              | B_Cte             |   211.2 μs |  1.7090 |      - |   15.7 KB |
| B_Nextorm_Cached_Cte                      | B_Cte             |   407.2 μs | 18.5547 |      - | 152.43 KB |
| B_Linq2Db_Cte                             | B_Cte             |   579.1 μs | 10.7422 |      - |  92.19 KB |
|                                           |                   |            |         |        |           |
| B_Nextorm_Cached_Distinct                 | B_Distinct        |   182.6 μs |  6.3477 |      - |  52.11 KB |
| B_Dapper_Distinct                         | B_Distinct        |   187.3 μs |  1.7090 |      - |  15.55 KB |
| B_Linq2Db_Distinct                        | B_Distinct        |   306.0 μs |  4.3945 |      - |  39.14 KB |
| B_EFCore_Distinct                         | B_Distinct        |   638.5 μs | 14.6484 | 4.8828 | 121.96 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_Except                           | B_Except          |   174.3 μs |  1.7090 |      - |  14.38 KB |
| B_Nextorm_Cached_Except                   | B_Except          |   215.4 μs | 10.4980 |      - |  86.49 KB |
| B_Linq2Db_Except                          | B_Except          |   378.0 μs |  5.8594 |      - |  50.55 KB |
| B_EFCore_Except                           | B_Except          |   738.4 μs | 18.5547 | 5.8594 | 153.05 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_In                               | B_InList          |   180.1 μs |  1.4648 |      - |  12.66 KB |
| B_Nextorm_Cached_In_AtIn_Inline           | B_InList          |   309.0 μs | 10.2539 |      - |  84.92 KB |
| B_Nextorm_Cached_In_AtIn_Captured         | B_InList          |   310.5 μs | 11.7188 |      - |  97.42 KB |
| B_Nextorm_Cached_In_ListContains_Inline   | B_InList          |   315.8 μs | 10.2539 |      - |  84.77 KB |
| B_Nextorm_Cached_In_ListContains_Captured | B_InList          |   326.2 μs | 11.7188 |      - |  97.89 KB |
| B_Linq2Db_In                              | B_InList          |   695.0 μs | 22.4609 |      - | 189.15 KB |
| B_EFCore_In                               | B_InList          | 1,055.4 μs | 27.3438 | 7.8125 | 226.12 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_Intersect                        | B_Intersect       |   173.7 μs |  1.4648 |      - |  12.89 KB |
| B_Nextorm_Cached_Intersect                | B_Intersect       |   201.7 μs | 10.4980 |      - |  86.33 KB |
| B_Linq2Db_Intersect                       | B_Intersect       |   372.4 μs |  5.8594 |      - |  50.08 KB |
| B_EFCore_Intersect                        | B_Intersect       |   704.3 μs | 17.5781 | 5.8594 | 143.75 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_Join4                            | B_Join4           |   263.7 μs |  2.4414 |      - |  20.24 KB |
| B_Nextorm_Cached_Join4                    | B_Join4           |   375.6 μs | 16.1133 |      - | 135.01 KB |
| B_Linq2Db_Join4                           | B_Join4           |   667.8 μs | 16.6016 |      - |  140.4 KB |
| B_EFCore_Join4                            | B_Join4           | 1,148.1 μs | 29.2969 | 7.8125 | 246.19 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_LeftJoin                         | B_LeftJoin        |   200.6 μs |  2.6855 |      - |  22.19 KB |
| B_Nextorm_Cached_LeftJoin                 | B_LeftJoin        |   255.6 μs |  8.7891 |      - |  75.47 KB |
| B_Linq2Db_LeftJoin                        | B_LeftJoin        |   687.5 μs |  9.7656 |      - |  86.02 KB |
| B_EFCore_LeftJoin                         | B_LeftJoin        |   987.1 μs | 23.4375 | 7.8125 | 203.22 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_RecursiveCte                     | B_RecursiveCte    |   239.8 μs |  1.4648 |      - |  15.31 KB |
| B_Nextorm_Cached_RecursiveCte             | B_RecursiveCte    |   424.1 μs | 20.0195 |      - | 163.83 KB |
| B_Linq2Db_RecursiveCte                    | B_RecursiveCte    |   630.3 μs |  9.7656 |      - |  86.18 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_ToUpper                          | B_StringFunctions |   143.4 μs |  1.4648 |      - |  12.81 KB |
| B_Nextorm_Cached_Contains                 | B_StringFunctions |   176.3 μs |  7.5684 |      - |  63.59 KB |
| B_Nextorm_Cached_ToUpper                  | B_StringFunctions |   179.0 μs |  7.8125 |      - |  64.84 KB |
| B_Dapper_Contains                         | B_StringFunctions |   192.7 μs |  1.9531 |      - |  17.19 KB |
| B_Linq2Db_Contains                        | B_StringFunctions |   318.3 μs |  5.3711 |      - |  47.81 KB |
| B_Linq2Db_ToUpper                         | B_StringFunctions |   474.9 μs |  7.8125 |      - |  63.91 KB |
| B_EFCore_ToUpper                          | B_StringFunctions |   687.9 μs | 15.6250 | 4.8828 | 133.52 KB |
| B_EFCore_Contains                         | B_StringFunctions |   689.0 μs | 16.6016 | 4.8828 | 140.09 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_Udf                              | B_Udf             |   143.8 μs |  1.4648 |      - |  12.81 KB |
| B_Nextorm_Cached_Udf                      | B_Udf             |   185.8 μs |  7.8125 |      - |  64.92 KB |
| B_Linq2Db_Udf                             | B_Udf             |   477.1 μs |  7.8125 |      - |  63.91 KB |
| B_EFCore_Udf                              | B_Udf             |   664.9 μs | 15.6250 | 4.8828 | 133.52 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_SumOver                          | B_Window          |   307.7 μs |  1.4648 |      - |  15.47 KB |
| B_Dapper_RowNumber                        | B_Window          |   310.6 μs |  1.4648 |      - |  15.39 KB |
| B_Nextorm_Cached_SumOver                  | B_Window          |   394.4 μs |  8.3008 |      - |  67.81 KB |
| B_Nextorm_Cached_RowNumber                | B_Window          |   399.2 μs |  7.8125 |      - |  66.41 KB |
| B_Linq2Db_SumOver                         | B_Window          |   624.6 μs |  6.8359 |      - |  58.21 KB |
| B_Linq2Db_RowNumber                       | B_Window          |   633.9 μs |  6.8359 |      - |  59.85 KB |
