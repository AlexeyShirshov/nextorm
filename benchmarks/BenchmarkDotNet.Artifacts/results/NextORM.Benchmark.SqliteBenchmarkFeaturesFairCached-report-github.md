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
| B_Nextorm_Cached_CaseWhen                 | B_CaseWhen        |   145.5 μs |  4.1504 |      - |  34.69 KB |
| B_Dapper_CaseWhen                         | B_CaseWhen        |   177.9 μs |  1.4648 |      - |  13.44 KB |
| B_Linq2Db_CaseWhen                        | B_CaseWhen        |   303.9 μs |  4.3945 |      - |  38.05 KB |
| B_EFCore_CaseWhen                         | B_CaseWhen        |   638.1 μs | 13.6719 | 5.8594 | 119.54 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_Cte                              | B_Cte             |   243.3 μs |  1.7090 |      - |   15.7 KB |
| B_Nextorm_Cached_Cte                      | B_Cte             |   386.1 μs | 14.1602 |      - | 116.49 KB |
| B_Linq2Db_Cte                             | B_Cte             |   731.9 μs | 10.7422 |      - |  90.63 KB |
|                                           |                   |            |         |        |           |
| B_Nextorm_Cached_Distinct                 | B_Distinct        |   181.4 μs |  4.8828 |      - |  41.56 KB |
| B_Dapper_Distinct                         | B_Distinct        |   199.5 μs |  1.7090 |      - |  15.55 KB |
| B_Linq2Db_Distinct                        | B_Distinct        |   338.0 μs |  4.3945 |      - |  39.14 KB |
| B_EFCore_Distinct                         | B_Distinct        |   710.1 μs | 14.6484 | 4.8828 | 121.96 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_Except                           | B_Except          |   184.3 μs |  1.7090 |      - |  14.38 KB |
| B_Nextorm_Cached_Except                   | B_Except          |   199.1 μs |  7.8125 |      - |  63.99 KB |
| B_Linq2Db_Except                          | B_Except          |   414.3 μs |  5.8594 |      - |  50.55 KB |
| B_EFCore_Except                           | B_Except          |   797.0 μs | 18.5547 | 5.8594 | 154.22 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_In                               | B_InList          |   205.6 μs |  1.4648 |      - |  12.66 KB |
| B_Nextorm_Cached_In_ListContains_Inline   | B_InList          |   374.6 μs |  9.7656 |      - |  80.94 KB |
| B_Nextorm_Cached_In_AtIn_Captured         | B_InList          |   380.6 μs | 11.7188 |      - |  98.13 KB |
| B_Nextorm_Cached_In_ListContains_Captured | B_InList          |   395.6 μs | 12.2070 |      - | 100.09 KB |
| B_Nextorm_Cached_In_AtIn_Inline           | B_InList          |   413.5 μs |  9.2773 |      - |  79.61 KB |
| B_Linq2Db_In                              | B_InList          |   802.7 μs | 22.4609 |      - | 189.15 KB |
| B_EFCore_In                               | B_InList          | 1,195.7 μs | 27.3438 | 7.8125 | 226.12 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_Intersect                        | B_Intersect       |   180.3 μs |  1.4648 |      - |  12.89 KB |
| B_Nextorm_Cached_Intersect                | B_Intersect       |   180.9 μs |  7.8125 |      - |  63.83 KB |
| B_Linq2Db_Intersect                       | B_Intersect       |   395.7 μs |  5.8594 |      - |  50.08 KB |
| B_EFCore_Intersect                        | B_Intersect       |   775.0 μs | 17.5781 | 5.8594 | 143.75 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_Join4                            | B_Join4           |   281.0 μs |  2.4414 |      - |  20.24 KB |
| B_Nextorm_Cached_Join4                    | B_Join4           |   439.2 μs | 13.6719 |      - | 114.47 KB |
| B_Linq2Db_Join4                           | B_Join4           |   709.5 μs | 16.6016 |      - | 140.87 KB |
| B_EFCore_Join4                            | B_Join4           | 1,303.1 μs | 29.2969 | 7.8125 |  250.8 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_LeftJoin                         | B_LeftJoin        |   211.0 μs |  2.6855 |      - |  22.19 KB |
| B_Nextorm_Cached_LeftJoin                 | B_LeftJoin        |   290.2 μs |  7.8125 |      - |  66.57 KB |
| B_Linq2Db_LeftJoin                        | B_LeftJoin        |   722.5 μs |  9.7656 |      - |  86.02 KB |
| B_EFCore_LeftJoin                         | B_LeftJoin        | 1,010.9 μs | 23.4375 | 7.8125 | 203.07 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_RecursiveCte                     | B_RecursiveCte    |   262.4 μs |  1.4648 |      - |  15.32 KB |
| B_Nextorm_Cached_RecursiveCte             | B_RecursiveCte    |   415.5 μs | 16.6016 |      - | 137.89 KB |
| B_Linq2Db_RecursiveCte                    | B_RecursiveCte    |   641.5 μs |  9.7656 |      - |   85.4 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_ToUpper                          | B_StringFunctions |   154.9 μs |  1.4648 |      - |  12.81 KB |
| B_Nextorm_Cached_Contains                 | B_StringFunctions |   164.5 μs |  5.3711 |      - |  44.77 KB |
| B_Nextorm_Cached_ToUpper                  | B_StringFunctions |   176.4 μs |  5.3711 |      - |  44.77 KB |
| B_Dapper_Contains                         | B_StringFunctions |   202.3 μs |  1.9531 |      - |  17.19 KB |
| B_Linq2Db_Contains                        | B_StringFunctions |   326.7 μs |  5.3711 |      - |  47.82 KB |
| B_Linq2Db_ToUpper                         | B_StringFunctions |   652.0 μs |  7.8125 |      - |  63.91 KB |
| B_EFCore_Contains                         | B_StringFunctions |   702.4 μs | 16.6016 | 4.8828 | 140.09 KB |
| B_EFCore_ToUpper                          | B_StringFunctions |   826.7 μs | 15.6250 | 4.8828 | 132.74 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_Udf                              | B_Udf             |   153.4 μs |  1.4648 |      - |  12.81 KB |
| B_Nextorm_Cached_Udf                      | B_Udf             |   176.7 μs |  5.3711 |      - |  45.63 KB |
| B_Linq2Db_Udf                             | B_Udf             |   508.0 μs |  7.8125 |      - |  63.91 KB |
| B_EFCore_Udf                              | B_Udf             |   716.8 μs | 15.6250 | 4.8828 | 132.74 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_SumOver                          | B_Window          |   318.8 μs |  1.4648 |      - |  15.47 KB |
| B_Dapper_RowNumber                        | B_Window          |   333.3 μs |  1.4648 |      - |  15.39 KB |
| B_Nextorm_Cached_SumOver                  | B_Window          |   381.3 μs |  6.3477 |      - |  55.71 KB |
| B_Nextorm_Cached_RowNumber                | B_Window          |   460.6 μs |  6.3477 |      - |  53.75 KB |
| B_Linq2Db_SumOver                         | B_Window          |   646.7 μs |  6.8359 |      - |  58.21 KB |
| B_Linq2Db_RowNumber                       | B_Window          |   682.8 μs |  6.8359 |      - |  59.85 KB |
