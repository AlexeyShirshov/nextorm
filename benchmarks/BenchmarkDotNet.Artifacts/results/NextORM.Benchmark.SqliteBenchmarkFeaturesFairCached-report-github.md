```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method                                    | Categories        | Mean       | Gen0    | Gen1   | Allocated |
|------------------------------------------ |------------------ |-----------:|--------:|-------:|----------:|
| B_Nextorm_Cached_CaseWhen                 | B_CaseWhen        |   131.9 μs |  3.9063 |      - |  35.63 KB |
| B_Dapper_CaseWhen                         | B_CaseWhen        |   149.2 μs |  1.4648 |      - |  13.44 KB |
| B_Linq2Db_CaseWhen                        | B_CaseWhen        |   266.1 μs |  4.3945 |      - |  38.05 KB |
| B_EFCore_CaseWhen                         | B_CaseWhen        |   618.3 μs | 13.6719 | 5.8594 | 119.54 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_Cte                              | B_Cte             |   237.4 μs |  1.7090 |      - |   15.7 KB |
| B_Nextorm_Cached_Cte                      | B_Cte             |   311.5 μs | 13.1836 |      - | 109.54 KB |
| B_Linq2Db_Cte                             | B_Cte             |   611.2 μs | 10.7422 |      - |  92.04 KB |
|                                           |                   |            |         |        |           |
| B_Nextorm_Cached_Distinct                 | B_Distinct        |   164.4 μs |  4.8828 |      - |  41.33 KB |
| B_Dapper_Distinct                         | B_Distinct        |   217.1 μs |  1.7090 |      - |  15.55 KB |
| B_Linq2Db_Distinct                        | B_Distinct        |   364.1 μs |  4.3945 |      - |  39.14 KB |
| B_EFCore_Distinct                         | B_Distinct        |   707.9 μs | 14.6484 | 4.8828 | 121.96 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_Except                           | B_Except          |   183.2 μs |  1.7090 |      - |  14.38 KB |
| B_Nextorm_Cached_Except                   | B_Except          |   186.1 μs |  8.0566 |      - |  66.33 KB |
| B_Linq2Db_Except                          | B_Except          |   407.7 μs |  5.8594 |      - |  50.55 KB |
| B_EFCore_Except                           | B_Except          |   785.2 μs | 18.5547 | 5.8594 | 153.05 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_In                               | B_InList          |   187.0 μs |  1.4648 |      - |  12.66 KB |
| B_Nextorm_Cached_In_AtIn_Inline           | B_InList          |   270.3 μs |  8.7891 |      - |  72.66 KB |
| B_Nextorm_Cached_In_ListContains_Inline   | B_InList          |   286.6 μs |  8.7891 |      - |  73.52 KB |
| B_Nextorm_Cached_In_AtIn_Captured         | B_InList          |   301.4 μs |  9.7656 |      - |  83.44 KB |
| B_Nextorm_Cached_In_ListContains_Captured | B_InList          |   318.7 μs | 10.2539 |      - |  84.69 KB |
| B_Linq2Db_In                              | B_InList          |   715.3 μs | 22.4609 |      - | 189.15 KB |
| B_EFCore_In                               | B_InList          | 1,092.9 μs | 27.3438 | 7.8125 |  227.6 KB |
|                                           |                   |            |         |        |           |
| B_Nextorm_Cached_Intersect                | B_Intersect       |   175.2 μs |  8.0566 |      - |  66.17 KB |
| B_Dapper_Intersect                        | B_Intersect       |   181.3 μs |  1.4648 |      - |  12.89 KB |
| B_Linq2Db_Intersect                       | B_Intersect       |   384.7 μs |  5.8594 |      - |  50.08 KB |
| B_EFCore_Intersect                        | B_Intersect       |   747.4 μs | 17.5781 | 5.8594 | 143.75 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_Join4                            | B_Join4           |   272.4 μs |  2.4414 |      - |  20.24 KB |
| B_Nextorm_Cached_Join4                    | B_Join4           |   314.2 μs | 12.6953 |      - | 106.49 KB |
| B_Linq2Db_Join4                           | B_Join4           |   696.4 μs | 16.6016 |      - | 141.26 KB |
| B_EFCore_Join4                            | B_Join4           | 1,187.8 μs | 29.2969 | 7.8125 | 246.27 KB |
|                                           |                   |            |         |        |           |
| B_Nextorm_Cached_LeftJoin                 | B_LeftJoin        |   211.2 μs |  7.0801 |      - |  58.75 KB |
| B_Dapper_LeftJoin                         | B_LeftJoin        |   214.0 μs |  2.6855 |      - |  22.19 KB |
| B_Linq2Db_LeftJoin                        | B_LeftJoin        |   722.7 μs |  9.7656 |      - |  86.02 KB |
| B_EFCore_LeftJoin                         | B_LeftJoin        |   994.8 μs | 23.4375 | 7.8125 | 203.06 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_RecursiveCte                     | B_RecursiveCte    |   255.6 μs |  1.4648 |      - |  15.31 KB |
| B_Nextorm_Cached_RecursiveCte             | B_RecursiveCte    |   387.4 μs | 14.6484 |      - | 123.36 KB |
| B_Linq2Db_RecursiveCte                    | B_RecursiveCte    |   631.7 μs |  9.7656 |      - |  83.91 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_ToUpper                          | B_StringFunctions |   144.1 μs |  1.4648 |      - |  12.81 KB |
| B_Nextorm_Cached_ToUpper                  | B_StringFunctions |   160.2 μs |  6.3477 |      - |  52.81 KB |
| B_Nextorm_Cached_Contains                 | B_StringFunctions |   183.7 μs |  6.3477 |      - |  52.34 KB |
| B_Dapper_Contains                         | B_StringFunctions |   203.5 μs |  1.9531 |      - |  17.19 KB |
| B_Linq2Db_Contains                        | B_StringFunctions |   332.6 μs |  5.3711 |      - |  47.81 KB |
| B_Linq2Db_ToUpper                         | B_StringFunctions |   541.6 μs |  7.8125 |      - |  63.91 KB |
| B_EFCore_ToUpper                          | B_StringFunctions |   670.9 μs | 15.6250 | 4.8828 | 132.74 KB |
| B_EFCore_Contains                         | B_StringFunctions |   691.9 μs | 16.6016 | 4.8828 | 140.09 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_Udf                              | B_Udf             |   148.6 μs |  1.4648 |      - |  12.81 KB |
| B_Nextorm_Cached_Udf                      | B_Udf             |   173.3 μs |  6.3477 |      - |  53.67 KB |
| B_Linq2Db_Udf                             | B_Udf             |   505.5 μs |  7.8125 |      - |  63.91 KB |
| B_EFCore_Udf                              | B_Udf             |   686.5 μs | 15.6250 | 4.8828 | 132.74 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_SumOver                          | B_Window          |   313.1 μs |  1.4648 |      - |  15.47 KB |
| B_Dapper_RowNumber                        | B_Window          |   336.7 μs |  1.4648 |      - |  15.39 KB |
| B_Nextorm_Cached_SumOver                  | B_Window          |   341.1 μs |  6.8359 |      - |   56.8 KB |
| B_Nextorm_Cached_RowNumber                | B_Window          |   345.2 μs |  6.3477 |      - |   54.3 KB |
| B_Linq2Db_SumOver                         | B_Window          |   649.6 μs |  6.8359 |      - |  58.21 KB |
| B_Linq2Db_RowNumber                       | B_Window          |   664.7 μs |  6.8359 |      - |  59.85 KB |
