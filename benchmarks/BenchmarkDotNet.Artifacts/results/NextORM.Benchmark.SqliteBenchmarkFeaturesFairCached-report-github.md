```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method                                    | Categories        | Mean       | Gen0    | Gen1   | Allocated |
|------------------------------------------ |------------------ |-----------:|--------:|-------:|----------:|
| B_Nextorm_Cached_CaseWhen                 | B_CaseWhen        |   122.5 μs |  2.4414 |      - |  20.39 KB |
| B_Dapper_CaseWhen                         | B_CaseWhen        |   152.0 μs |  1.4648 |      - |  13.36 KB |
| B_Linq2Db_CaseWhen                        | B_CaseWhen        |   240.1 μs |  4.3945 |      - |  37.74 KB |
| B_EFCore_CaseWhen                         | B_CaseWhen        |   563.8 μs | 13.6719 | 5.8594 | 114.62 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_Cte                              | B_Cte             |   206.6 μs |  1.7090 |      - |  15.63 KB |
| B_Nextorm_Cached_Cte                      | B_Cte             |   305.9 μs | 11.2305 |      - |  92.51 KB |
| B_Linq2Db_Cte                             | B_Cte             |   552.1 μs | 10.7422 |      - |  91.65 KB |
|                                           |                   |            |         |        |           |
| B_Nextorm_Cached_Distinct                 | B_Distinct        |   149.1 μs |  3.6621 |      - |  30.08 KB |
| B_Dapper_Distinct                         | B_Distinct        |   176.7 μs |  1.7090 |      - |  14.45 KB |
| B_Linq2Db_Distinct                        | B_Distinct        |   302.6 μs |  4.3945 |      - |  38.75 KB |
| B_EFCore_Distinct                         | B_Distinct        |   610.4 μs | 13.6719 | 6.8359 | 116.96 KB |
|                                           |                   |            |         |        |           |
| B_Nextorm_Cached_Except                   | B_Except          |   163.3 μs |  4.8828 |      - |  41.64 KB |
| B_Dapper_Except                           | B_Except          |   177.5 μs |  1.7090 |      - |   14.3 KB |
| B_Linq2Db_Except                          | B_Except          |   373.0 μs |  5.8594 |      - |  50.08 KB |
| B_EFCore_Except                           | B_Except          |   717.8 μs | 17.5781 | 5.8594 | 147.35 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_In                               | B_InList          |   182.1 μs |  1.4648 |      - |  12.58 KB |
| B_Nextorm_Cached_In_AtIn_Inline           | B_InList          |   396.8 μs | 10.2539 |      - |  87.35 KB |
| B_Nextorm_Cached_In_AtIn_Captured         | B_InList          |   408.2 μs | 12.6953 |      - | 104.46 KB |
| B_Nextorm_Cached_In_ListContains_Inline   | B_InList          |   429.6 μs | 10.7422 |      - |  88.99 KB |
| B_Nextorm_Cached_In_ListContains_Captured | B_InList          |   432.7 μs | 12.6953 |      - | 106.73 KB |
| B_Linq2Db_In                              | B_InList          |   684.4 μs | 20.5078 |      - |  174.3 KB |
| B_EFCore_In                               | B_InList          | 1,049.0 μs | 25.3906 | 7.8125 | 220.49 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_Intersect                        | B_Intersect       |   169.1 μs |  1.4648 |      - |  12.81 KB |
| B_Nextorm_Cached_Intersect                | B_Intersect       |   172.6 μs |  4.8828 |      - |  41.49 KB |
| B_Linq2Db_Intersect                       | B_Intersect       |   374.8 μs |  5.8594 |      - |  49.61 KB |
| B_EFCore_Intersect                        | B_Intersect       |   727.5 μs | 16.6016 | 4.8828 | 138.05 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_Join4                            | B_Join4           |   261.6 μs |  2.4414 |      - |  20.16 KB |
| B_Nextorm_Cached_Join4                    | B_Join4           |   326.5 μs | 11.7188 |      - |   99.7 KB |
| B_Linq2Db_Join4                           | B_Join4           |   660.7 μs | 16.6016 |      - | 141.18 KB |
| B_EFCore_Join4                            | B_Join4           | 1,120.4 μs | 27.3438 | 7.8125 | 238.22 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_LeftJoin                         | B_LeftJoin        |   202.0 μs |  2.6855 |      - |  22.11 KB |
| B_Nextorm_Cached_LeftJoin                 | B_LeftJoin        |   230.5 μs |  6.8359 |      - |  57.03 KB |
| B_Linq2Db_LeftJoin                        | B_LeftJoin        |   682.1 μs | 10.7422 |      - |  89.38 KB |
| B_EFCore_LeftJoin                         | B_LeftJoin        |   958.1 μs | 23.4375 | 7.8125 | 196.58 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_RecursiveCte                     | B_RecursiveCte    |   243.3 μs |  1.4648 |      - |  15.24 KB |
| B_Nextorm_Cached_RecursiveCte             | B_RecursiveCte    |   335.8 μs | 12.2070 |      - | 100.71 KB |
| B_Linq2Db_RecursiveCte                    | B_RecursiveCte    |   589.7 μs |  9.7656 |      - |  83.13 KB |
|                                           |                   |            |         |        |           |
| B_Nextorm_Cached_Contains                 | B_StringFunctions |   130.5 μs |  3.1738 |      - |  27.81 KB |
| B_Nextorm_Cached_ToUpper                  | B_StringFunctions |   134.3 μs |  3.4180 |      - |  28.36 KB |
| B_Dapper_ToUpper                          | B_StringFunctions |   142.6 μs |  1.4648 |      - |  12.74 KB |
| B_Dapper_Contains                         | B_StringFunctions |   196.2 μs |  1.9531 |      - |  17.11 KB |
| B_Linq2Db_Contains                        | B_StringFunctions |   318.7 μs |  5.8594 |      - |  48.91 KB |
| B_Linq2Db_ToUpper                         | B_StringFunctions |   476.1 μs |  8.3008 |      - |  68.21 KB |
| B_EFCore_ToUpper                          | B_StringFunctions |   652.8 μs | 14.6484 | 4.8828 | 127.43 KB |
| B_EFCore_Contains                         | B_StringFunctions |   654.1 μs | 15.6250 | 4.8828 | 134.77 KB |
|                                           |                   |            |         |        |           |
| B_Nextorm_Cached_Udf                      | B_Udf             |   140.3 μs |  3.4180 |      - |  28.67 KB |
| B_Dapper_Udf                              | B_Udf             |   143.0 μs |  1.4648 |      - |  12.74 KB |
| B_Linq2Db_Udf                             | B_Udf             |   474.8 μs |  8.3008 |      - |  68.21 KB |
| B_EFCore_Udf                              | B_Udf             |   646.0 μs | 14.6484 | 4.8828 | 127.43 KB |
|                                           |                   |            |         |        |           |
| B_Nextorm_Cached_SumOver                  | B_Window          |   276.6 μs |  4.8828 |      - |  41.64 KB |
| B_Nextorm_Cached_RowNumber                | B_Window          |   278.6 μs |  4.8828 |      - |  40.24 KB |
| B_Dapper_RowNumber                        | B_Window          |   298.9 μs |  1.4648 |      - |  15.32 KB |
| B_Dapper_SumOver                          | B_Window          |   316.4 μs |  1.4648 |      - |  15.39 KB |
| B_Linq2Db_SumOver                         | B_Window          |   610.1 μs |  6.8359 |      - |   57.9 KB |
| B_Linq2Db_RowNumber                       | B_Window          |   651.5 μs |  6.8359 |      - |  60.63 KB |
