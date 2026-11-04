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
| B_Nextorm_Cached_CaseWhen                 | B_CaseWhen        |   130.4 μs |  3.9063 |      - |  32.19 KB |
| B_Dapper_CaseWhen                         | B_CaseWhen        |   140.0 μs |  1.4648 |      - |  13.44 KB |
| B_Linq2Db_CaseWhen                        | B_CaseWhen        |   250.5 μs |  4.3945 |      - |  38.05 KB |
| B_EFCore_CaseWhen                         | B_CaseWhen        |   567.8 μs | 13.6719 | 6.8359 | 119.54 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_Cte                              | B_Cte             |   198.3 μs |  1.7090 |      - |   15.7 KB |
| B_Nextorm_Cached_Cte                      | B_Cte             |   327.2 μs | 13.1836 |      - | 108.91 KB |
| B_Linq2Db_Cte                             | B_Cte             |   568.9 μs | 10.7422 |      - |  90.63 KB |
|                                           |                   |            |         |        |           |
| B_Nextorm_Cached_Distinct                 | B_Distinct        |   155.3 μs |  4.6387 |      - |  37.97 KB |
| B_Dapper_Distinct                         | B_Distinct        |   184.1 μs |  1.7090 |      - |  15.55 KB |
| B_Linq2Db_Distinct                        | B_Distinct        |   304.4 μs |  4.3945 |      - |  39.14 KB |
| B_EFCore_Distinct                         | B_Distinct        |   641.9 μs | 14.6484 | 4.8828 | 121.96 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_Except                           | B_Except          |   169.6 μs |  1.7090 |      - |  14.38 KB |
| B_Nextorm_Cached_Except                   | B_Except          |   171.5 μs |  7.5684 |      - |  63.75 KB |
| B_Linq2Db_Except                          | B_Except          |   381.0 μs |  5.8594 |      - |  50.55 KB |
| B_EFCore_Except                           | B_Except          |   728.2 μs | 18.5547 | 5.8594 | 153.05 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_In                               | B_InList          |   181.2 μs |  1.4648 |      - |  12.66 KB |
| B_Nextorm_Cached_In_AtIn_Inline           | B_InList          |   316.5 μs |  9.2773 |      - |  77.58 KB |
| B_Nextorm_Cached_In_AtIn_Captured         | B_InList          |   340.7 μs | 10.7422 |      - |  90.16 KB |
| B_Nextorm_Cached_In_ListContains_Inline   | B_InList          |   344.3 μs |  9.2773 |      - |  78.91 KB |
| B_Nextorm_Cached_In_ListContains_Captured | B_InList          |   348.5 μs | 11.2305 |      - |  92.11 KB |
| B_Linq2Db_In                              | B_InList          |   694.4 μs | 22.4609 |      - | 189.15 KB |
| B_EFCore_In                               | B_InList          | 1,126.3 μs | 27.3438 | 7.8125 | 226.11 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_Intersect                        | B_Intersect       |   156.0 μs |  1.4648 |      - |  12.89 KB |
| B_Nextorm_Cached_Intersect                | B_Intersect       |   167.9 μs |  7.5684 |      - |  63.59 KB |
| B_Linq2Db_Intersect                       | B_Intersect       |   367.7 μs |  5.8594 |      - |  50.08 KB |
| B_EFCore_Intersect                        | B_Intersect       |   719.4 μs | 17.5781 | 5.8594 | 143.75 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_Join4                            | B_Join4           |   250.3 μs |  2.4414 |      - |  20.24 KB |
| B_Nextorm_Cached_Join4                    | B_Join4           |   329.6 μs | 12.6953 |      - | 106.73 KB |
| B_Linq2Db_Join4                           | B_Join4           |   653.8 μs | 16.6016 |      - | 141.03 KB |
| B_EFCore_Join4                            | B_Join4           | 1,142.0 μs | 29.2969 | 9.7656 | 246.89 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_LeftJoin                         | B_LeftJoin        |   198.3 μs |  2.6855 |      - |  22.19 KB |
| B_Nextorm_Cached_LeftJoin                 | B_LeftJoin        |   230.3 μs |  7.3242 |      - |  61.64 KB |
| B_Linq2Db_LeftJoin                        | B_LeftJoin        |   691.2 μs |  9.7656 |      - |  86.02 KB |
| B_EFCore_LeftJoin                         | B_LeftJoin        |   948.0 μs | 24.4141 | 7.8125 | 202.75 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_RecursiveCte                     | B_RecursiveCte    |   244.4 μs |  1.7090 |      - |  15.31 KB |
| B_Nextorm_Cached_RecursiveCte             | B_RecursiveCte    |   353.3 μs | 16.1133 |      - | 133.13 KB |
| B_Linq2Db_RecursiveCte                    | B_RecursiveCte    |   604.1 μs |  9.7656 |      - |  83.91 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_ToUpper                          | B_StringFunctions |   139.3 μs |  1.4648 |      - |  12.81 KB |
| B_Nextorm_Cached_Contains                 | B_StringFunctions |   149.7 μs |  5.1270 |      - |  42.03 KB |
| B_Nextorm_Cached_ToUpper                  | B_StringFunctions |   152.9 μs |  5.1270 |      - |  42.03 KB |
| B_Dapper_Contains                         | B_StringFunctions |   181.8 μs |  1.9531 |      - |  17.19 KB |
| B_Linq2Db_Contains                        | B_StringFunctions |   309.5 μs |  5.3711 |      - |  47.81 KB |
| B_Linq2Db_ToUpper                         | B_StringFunctions |   477.5 μs |  7.8125 |      - |     65 KB |
| B_EFCore_ToUpper                          | B_StringFunctions |   662.6 μs | 15.6250 | 4.8828 | 132.74 KB |
| B_EFCore_Contains                         | B_StringFunctions |   695.5 μs | 16.6016 | 4.8828 | 140.09 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_Udf                              | B_Udf             |   142.9 μs |  1.4648 |      - |  12.81 KB |
| B_Nextorm_Cached_Udf                      | B_Udf             |   155.3 μs |  5.1270 |      - |  42.89 KB |
| B_Linq2Db_Udf                             | B_Udf             |   484.6 μs |  7.8125 |      - |     65 KB |
| B_EFCore_Udf                              | B_Udf             |   662.5 μs | 15.6250 | 4.8828 | 132.74 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_SumOver                          | B_Window          |   297.5 μs |  1.4648 |      - |  15.47 KB |
| B_Dapper_RowNumber                        | B_Window          |   304.6 μs |  1.4648 |      - |  15.39 KB |
| B_Nextorm_Cached_RowNumber                | B_Window          |   315.5 μs |  5.8594 |      - |  50.31 KB |
| B_Nextorm_Cached_SumOver                  | B_Window          |   330.3 μs |  6.3477 |      - |  54.61 KB |
| B_Linq2Db_RowNumber                       | B_Window          |   621.1 μs |  6.8359 |      - |  59.85 KB |
| B_Linq2Db_SumOver                         | B_Window          |   622.8 μs |  6.8359 |      - |  58.21 KB |
