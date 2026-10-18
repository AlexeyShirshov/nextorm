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
| B_Nextorm_Cached_CaseWhen                 | B_CaseWhen        |   133.3 μs |  3.9063 |      - |  32.19 KB |
| B_Dapper_CaseWhen                         | B_CaseWhen        |   153.7 μs |  1.4648 |      - |  13.44 KB |
| B_Linq2Db_CaseWhen                        | B_CaseWhen        |   268.9 μs |  4.3945 |      - |  38.05 KB |
| B_EFCore_CaseWhen                         | B_CaseWhen        |   607.2 μs | 13.6719 | 5.8594 | 119.54 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_Cte                              | B_Cte             |   226.7 μs |  1.7090 |      - |   15.7 KB |
| B_Nextorm_Cached_Cte                      | B_Cte             |   327.8 μs | 13.1836 |      - | 108.91 KB |
| B_Linq2Db_Cte                             | B_Cte             |   604.0 μs | 10.7422 |      - |  91.73 KB |
|                                           |                   |            |         |        |           |
| B_Nextorm_Cached_Distinct                 | B_Distinct        |   165.2 μs |  4.6387 |      - |  37.97 KB |
| B_Dapper_Distinct                         | B_Distinct        |   199.0 μs |  1.7090 |      - |  15.55 KB |
| B_Linq2Db_Distinct                        | B_Distinct        |   331.1 μs |  4.3945 |      - |  39.14 KB |
| B_EFCore_Distinct                         | B_Distinct        |   700.0 μs | 14.6484 | 4.8828 | 121.96 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_Except                           | B_Except          |   185.4 μs |  1.7090 |      - |  14.38 KB |
| B_Nextorm_Cached_Except                   | B_Except          |   187.6 μs |  7.5684 |      - |  63.75 KB |
| B_Linq2Db_Except                          | B_Except          |   413.5 μs |  5.8594 |      - |  50.55 KB |
| B_EFCore_Except                           | B_Except          |   791.3 μs | 18.5547 | 5.8594 | 153.05 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_In                               | B_InList          |   194.7 μs |  1.4648 |      - |  12.66 KB |
| B_Nextorm_Cached_In_AtIn_Inline           | B_InList          |   343.4 μs |  9.2773 |      - |  77.58 KB |
| B_Nextorm_Cached_In_AtIn_Captured         | B_InList          |   356.4 μs | 10.7422 |      - |  90.16 KB |
| B_Nextorm_Cached_In_ListContains_Inline   | B_InList          |   364.6 μs |  9.2773 |      - |  78.91 KB |
| B_Nextorm_Cached_In_ListContains_Captured | B_InList          |   367.8 μs | 11.2305 |      - |  92.11 KB |
| B_Linq2Db_In                              | B_InList          |   731.5 μs | 22.4609 |      - | 189.15 KB |
| B_EFCore_In                               | B_InList          | 1,119.3 μs | 27.3438 | 7.8125 | 227.68 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_Intersect                        | B_Intersect       |   172.7 μs |  1.4648 |      - |  12.89 KB |
| B_Nextorm_Cached_Intersect                | B_Intersect       |   183.6 μs |  7.5684 |      - |  63.59 KB |
| B_Linq2Db_Intersect                       | B_Intersect       |   390.7 μs |  5.8594 |      - |  50.08 KB |
| B_EFCore_Intersect                        | B_Intersect       |   766.4 μs | 17.5781 | 5.8594 | 143.75 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_Join4                            | B_Join4           |   275.4 μs |  2.4414 |      - |  20.24 KB |
| B_Nextorm_Cached_Join4                    | B_Join4           |   353.0 μs | 12.6953 |      - | 107.51 KB |
| B_Linq2Db_Join4                           | B_Join4           |   710.8 μs | 17.5781 |      - | 143.61 KB |
| B_EFCore_Join4                            | B_Join4           | 1,222.3 μs | 29.2969 | 7.8125 | 246.82 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_LeftJoin                         | B_LeftJoin        |   215.9 μs |  2.6855 |      - |  22.19 KB |
| B_Nextorm_Cached_LeftJoin                 | B_LeftJoin        |   249.1 μs |  7.3242 |      - |  61.64 KB |
| B_Linq2Db_LeftJoin                        | B_LeftJoin        |   729.5 μs |  9.7656 |      - |  86.02 KB |
| B_EFCore_LeftJoin                         | B_LeftJoin        | 1,024.0 μs | 23.4375 | 7.8125 | 203.06 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_RecursiveCte                     | B_RecursiveCte    |   258.6 μs |  1.4648 |      - |  15.31 KB |
| B_Nextorm_Cached_RecursiveCte             | B_RecursiveCte    |   390.3 μs | 16.6016 |      - | 135.63 KB |
| B_Linq2Db_RecursiveCte                    | B_RecursiveCte    |   650.7 μs |  9.7656 |      - |  85.48 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_ToUpper                          | B_StringFunctions |   150.8 μs |  1.4648 |      - |  12.81 KB |
| B_Nextorm_Cached_ToUpper                  | B_StringFunctions |   161.6 μs |  5.1270 |      - |  42.03 KB |
| B_Nextorm_Cached_Contains                 | B_StringFunctions |   181.3 μs |  5.1270 |      - |  42.03 KB |
| B_Dapper_Contains                         | B_StringFunctions |   200.1 μs |  1.9531 |      - |  17.19 KB |
| B_Linq2Db_Contains                        | B_StringFunctions |   343.4 μs |  5.8594 |      - |  48.91 KB |
| B_Linq2Db_ToUpper                         | B_StringFunctions |   510.4 μs |  7.8125 |      - |  63.91 KB |
| B_EFCore_ToUpper                          | B_StringFunctions |   706.4 μs | 15.6250 | 4.8828 | 132.74 KB |
| B_EFCore_Contains                         | B_StringFunctions |   763.4 μs | 16.6016 | 4.8828 | 140.08 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_Udf                              | B_Udf             |   151.0 μs |  1.4648 |      - |  12.81 KB |
| B_Nextorm_Cached_Udf                      | B_Udf             |   162.8 μs |  5.1270 |      - |  42.89 KB |
| B_Linq2Db_Udf                             | B_Udf             |   508.7 μs |  7.8125 |      - |  63.91 KB |
| B_EFCore_Udf                              | B_Udf             |   705.2 μs | 15.6250 | 4.8828 | 132.74 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_SumOver                          | B_Window          |   318.9 μs |  1.4648 |      - |  15.47 KB |
| B_Dapper_RowNumber                        | B_Window          |   323.1 μs |  1.4648 |      - |  15.39 KB |
| B_Nextorm_Cached_RowNumber                | B_Window          |   339.1 μs |  5.8594 |      - |  50.31 KB |
| B_Nextorm_Cached_SumOver                  | B_Window          |   346.0 μs |  6.3477 |      - |  52.27 KB |
| B_Linq2Db_RowNumber                       | B_Window          |   663.0 μs |  6.8359 |      - |  59.85 KB |
| B_Linq2Db_SumOver                         | B_Window          |   671.2 μs |  6.8359 |      - |   59.3 KB |
