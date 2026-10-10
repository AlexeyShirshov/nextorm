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
| B_Nextorm_Cached_CaseWhen                 | B_CaseWhen        |   140.1 μs |  4.8828 |      - |  40.39 KB |
| B_Dapper_CaseWhen                         | B_CaseWhen        |   150.5 μs |  1.4648 |      - |  13.44 KB |
| B_Linq2Db_CaseWhen                        | B_CaseWhen        |   269.0 μs |  4.3945 |      - |  38.05 KB |
| B_EFCore_CaseWhen                         | B_CaseWhen        |   615.9 μs | 13.6719 | 6.8359 | 119.54 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_Cte                              | B_Cte             |   221.5 μs |  1.7090 |      - |   15.7 KB |
| B_Nextorm_Cached_Cte                      | B_Cte             |   443.5 μs | 16.6016 |      - | 138.45 KB |
| B_Linq2Db_Cte                             | B_Cte             |   601.1 μs | 10.7422 |      - |  90.63 KB |
|                                           |                   |            |         |        |           |
| B_Nextorm_Cached_Distinct                 | B_Distinct        |   186.6 μs |  5.8594 |      - |  48.28 KB |
| B_Dapper_Distinct                         | B_Distinct        |   203.5 μs |  1.7090 |      - |  15.55 KB |
| B_Linq2Db_Distinct                        | B_Distinct        |   332.0 μs |  4.3945 |      - |  39.14 KB |
| B_EFCore_Distinct                         | B_Distinct        |   666.3 μs | 14.6484 | 4.8828 | 121.96 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_Except                           | B_Except          |   184.5 μs |  1.7090 |      - |  14.38 KB |
| B_Nextorm_Cached_Except                   | B_Except          |   225.1 μs |  9.7656 |      - |  80.78 KB |
| B_Linq2Db_Except                          | B_Except          |   406.6 μs |  5.8594 |      - |  50.55 KB |
| B_EFCore_Except                           | B_Except          |   782.6 μs | 18.5547 | 5.8594 | 153.05 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_In                               | B_InList          |   191.4 μs |  1.4648 |      - |  12.66 KB |
| B_Nextorm_Cached_In_AtIn_Captured         | B_InList          |   326.4 μs | 10.7422 |      - |  91.17 KB |
| B_Nextorm_Cached_In_AtIn_Inline           | B_InList          |   328.0 μs |  9.2773 |      - |  79.53 KB |
| B_Nextorm_Cached_In_ListContains_Inline   | B_InList          |   341.9 μs |  9.2773 |      - |  79.38 KB |
| B_Nextorm_Cached_In_ListContains_Captured | B_InList          |   343.3 μs | 10.7422 |      - |  91.64 KB |
| B_Linq2Db_In                              | B_InList          |   727.7 μs | 22.4609 |      - | 190.71 KB |
| B_EFCore_In                               | B_InList          | 1,101.8 μs | 27.3438 | 7.8125 | 226.12 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_Intersect                        | B_Intersect       |   173.8 μs |  1.4648 |      - |  12.89 KB |
| B_Nextorm_Cached_Intersect                | B_Intersect       |   212.6 μs |  9.7656 |      - |  80.63 KB |
| B_Linq2Db_Intersect                       | B_Intersect       |   389.0 μs |  5.8594 |      - |  50.08 KB |
| B_EFCore_Intersect                        | B_Intersect       |   761.6 μs | 17.5781 | 5.8594 | 143.75 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_Join4                            | B_Join4           |   272.2 μs |  2.4414 |      - |  20.24 KB |
| B_Nextorm_Cached_Join4                    | B_Join4           |   439.2 μs | 15.6250 |      - | 130.95 KB |
| B_Linq2Db_Join4                           | B_Join4           |   713.9 μs | 16.6016 |      - | 140.56 KB |
| B_EFCore_Join4                            | B_Join4           | 1,210.5 μs | 29.2969 | 9.7656 | 245.18 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_LeftJoin                         | B_LeftJoin        |   210.0 μs |  2.6855 |      - |  22.19 KB |
| B_Nextorm_Cached_LeftJoin                 | B_LeftJoin        |   293.1 μs |  9.2773 |      - |  76.96 KB |
| B_Linq2Db_LeftJoin                        | B_LeftJoin        |   726.8 μs |  9.7656 |      - |  87.27 KB |
| B_EFCore_LeftJoin                         | B_LeftJoin        | 1,008.5 μs | 23.4375 | 7.8125 | 203.92 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_RecursiveCte                     | B_RecursiveCte    |   254.8 μs |  1.4648 |      - |  15.31 KB |
| B_Nextorm_Cached_RecursiveCte             | B_RecursiveCte    |   461.0 μs | 19.5313 |      - | 160.94 KB |
| B_Linq2Db_RecursiveCte                    | B_RecursiveCte    |   637.3 μs |  9.7656 |      - |  83.91 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_ToUpper                          | B_StringFunctions |   145.2 μs |  1.4648 |      - |  12.81 KB |
| B_Nextorm_Cached_ToUpper                  | B_StringFunctions |   177.8 μs |  6.8359 |      - |  57.03 KB |
| B_Nextorm_Cached_Contains                 | B_StringFunctions |   178.7 μs |  6.8359 |      - |  56.56 KB |
| B_Dapper_Contains                         | B_StringFunctions |   205.1 μs |  1.9531 |      - |  17.19 KB |
| B_Linq2Db_Contains                        | B_StringFunctions |   324.4 μs |  5.3711 |      - |  47.81 KB |
| B_Linq2Db_ToUpper                         | B_StringFunctions |   507.7 μs |  7.8125 |      - |  63.91 KB |
| B_EFCore_ToUpper                          | B_StringFunctions |   679.5 μs | 15.6250 | 4.8828 | 132.74 KB |
| B_EFCore_Contains                         | B_StringFunctions |   706.3 μs | 16.6016 | 4.8828 | 140.09 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_Udf                              | B_Udf             |   151.6 μs |  1.4648 |      - |  12.81 KB |
| B_Nextorm_Cached_Udf                      | B_Udf             |   187.8 μs |  7.0801 |      - |  57.89 KB |
| B_Linq2Db_Udf                             | B_Udf             |   518.4 μs |  7.8125 |      - |  63.91 KB |
| B_EFCore_Udf                              | B_Udf             |   679.4 μs | 15.6250 | 4.8828 | 132.74 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_SumOver                          | B_Window          |   324.1 μs |  1.4648 |      - |  15.47 KB |
| B_Dapper_RowNumber                        | B_Window          |   330.9 μs |  1.4648 |      - |  15.39 KB |
| B_Nextorm_Cached_RowNumber                | B_Window          |   438.1 μs |  7.3242 |      - |  61.17 KB |
| B_Nextorm_Cached_SumOver                  | B_Window          |   449.9 μs |  7.3242 |      - |  62.58 KB |
| B_Linq2Db_SumOver                         | B_Window          |   702.9 μs |  6.8359 |      - |  58.99 KB |
| B_Linq2Db_RowNumber                       | B_Window          |   728.2 μs |  6.8359 |      - |  59.85 KB |
