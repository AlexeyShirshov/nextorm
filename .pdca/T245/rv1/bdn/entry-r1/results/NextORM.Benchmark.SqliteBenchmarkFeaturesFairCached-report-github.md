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
| B_Nextorm_Cached_CaseWhen                 | B_CaseWhen        |   152.8 μs |  3.9063 |      - |  32.19 KB |
| B_Dapper_CaseWhen                         | B_CaseWhen        |   167.5 μs |  1.4648 |      - |  13.44 KB |
| B_Linq2Db_CaseWhen                        | B_CaseWhen        |   312.2 μs |  4.3945 |      - |  38.05 KB |
| B_EFCore_CaseWhen                         | B_CaseWhen        |   713.9 μs | 13.6719 | 5.8594 | 119.54 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_Cte                              | B_Cte             |   251.0 μs |  1.4648 |      - |  15.71 KB |
| B_Nextorm_Cached_Cte                      | B_Cte             |   432.4 μs | 13.1836 |      - |  110.4 KB |
| B_Linq2Db_Cte                             | B_Cte             |   705.0 μs | 10.7422 |      - |  90.63 KB |
|                                           |                   |            |         |        |           |
| B_Nextorm_Cached_Distinct                 | B_Distinct        |   182.4 μs |  4.6387 |      - |  37.97 KB |
| B_Dapper_Distinct                         | B_Distinct        |   230.1 μs |  1.7090 |      - |  15.55 KB |
| B_Linq2Db_Distinct                        | B_Distinct        |   397.1 μs |  4.8828 |      - |  40.31 KB |
| B_EFCore_Distinct                         | B_Distinct        |   747.6 μs | 14.6484 | 4.8828 | 121.96 KB |
|                                           |                   |            |         |        |           |
| B_Nextorm_Cached_Except                   | B_Except          |   210.2 μs |  7.5684 |      - |  63.75 KB |
| B_Dapper_Except                           | B_Except          |   246.5 μs |  1.7090 |      - |  14.38 KB |
| B_Linq2Db_Except                          | B_Except          |   567.8 μs |  5.8594 |      - |  50.55 KB |
| B_EFCore_Except                           | B_Except          |   895.9 μs | 18.5547 | 5.8594 | 153.05 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_In                               | B_InList          |   329.7 μs |  1.4648 |      - |  12.66 KB |
| B_Nextorm_Cached_In_AtIn_Captured         | B_InList          |   473.0 μs | 10.7422 |      - |  90.16 KB |
| B_Nextorm_Cached_In_AtIn_Inline           | B_InList          |   506.8 μs |  8.7891 |      - |  77.58 KB |
| B_Nextorm_Cached_In_ListContains_Inline   | B_InList          |   544.5 μs |  8.7891 |      - |  78.91 KB |
| B_Nextorm_Cached_In_ListContains_Captured | B_InList          |   554.5 μs | 10.7422 |      - |  92.11 KB |
| B_Linq2Db_In                              | B_InList          | 1,121.6 μs | 21.4844 |      - | 189.15 KB |
| B_EFCore_In                               | B_InList          | 1,541.1 μs | 27.3438 | 7.8125 | 226.12 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_Intersect                        | B_Intersect       |   231.2 μs |  1.4648 |      - |  12.89 KB |
| B_Nextorm_Cached_Intersect                | B_Intersect       |   248.5 μs |  7.5684 |      - |  63.59 KB |
| B_Linq2Db_Intersect                       | B_Intersect       |   568.5 μs |  5.8594 |      - |  50.08 KB |
| B_EFCore_Intersect                        | B_Intersect       | 1,037.7 μs | 17.5781 | 5.8594 | 143.75 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_Join4                            | B_Join4           |   394.1 μs |  2.4414 |      - |  20.24 KB |
| B_Nextorm_Cached_Join4                    | B_Join4           |   445.7 μs | 12.6953 |      - | 107.51 KB |
| B_Linq2Db_Join4                           | B_Join4           |   864.1 μs | 16.6016 |      - | 141.34 KB |
| B_EFCore_Join4                            | B_Join4           | 1,630.6 μs | 29.2969 | 7.8125 | 246.66 KB |
|                                           |                   |            |         |        |           |
| B_Nextorm_Cached_LeftJoin                 | B_LeftJoin        |   297.7 μs |  7.3242 |      - |  61.64 KB |
| B_Dapper_LeftJoin                         | B_LeftJoin        |   304.3 μs |  2.4414 |      - |  22.19 KB |
| B_Linq2Db_LeftJoin                        | B_LeftJoin        | 1,048.2 μs |  9.7656 |      - |  86.02 KB |
| B_EFCore_LeftJoin                         | B_LeftJoin        | 1,150.4 μs | 23.4375 | 7.8125 | 201.97 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_RecursiveCte                     | B_RecursiveCte    |   338.4 μs |  1.4648 |      - |  15.31 KB |
| B_Nextorm_Cached_RecursiveCte             | B_RecursiveCte    |   487.4 μs | 16.1133 |      - | 134.22 KB |
| B_Linq2Db_RecursiveCte                    | B_RecursiveCte    |   715.4 μs |  9.7656 |      - |  83.91 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_ToUpper                          | B_StringFunctions |   176.0 μs |  1.4648 |      - |  12.81 KB |
| B_Nextorm_Cached_Contains                 | B_StringFunctions |   182.2 μs |  5.1270 |      - |  42.03 KB |
| B_Nextorm_Cached_ToUpper                  | B_StringFunctions |   188.8 μs |  5.1270 |      - |  42.03 KB |
| B_Dapper_Contains                         | B_StringFunctions |   268.9 μs |  1.9531 |      - |  17.19 KB |
| B_Linq2Db_Contains                        | B_StringFunctions |   413.2 μs |  5.3711 |      - |  47.81 KB |
| B_Linq2Db_ToUpper                         | B_StringFunctions |   571.4 μs |  7.8125 |      - |  63.91 KB |
| B_EFCore_ToUpper                          | B_StringFunctions |   757.3 μs | 15.6250 | 4.8828 | 132.74 KB |
| B_EFCore_Contains                         | B_StringFunctions |   773.5 μs | 16.6016 | 4.8828 | 140.09 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_Udf                              | B_Udf             |   172.5 μs |  1.4648 |      - |  12.81 KB |
| B_Nextorm_Cached_Udf                      | B_Udf             |   216.1 μs |  5.1270 |      - |  42.89 KB |
| B_Linq2Db_Udf                             | B_Udf             |   585.4 μs |  7.8125 |      - |  63.91 KB |
| B_EFCore_Udf                              | B_Udf             |   968.9 μs | 15.6250 | 4.8828 | 132.74 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_SumOver                          | B_Window          |   370.3 μs |  1.4648 |      - |  15.47 KB |
| B_Nextorm_Cached_SumOver                  | B_Window          |   409.0 μs |  6.3477 |      - |  52.27 KB |
| B_Nextorm_Cached_RowNumber                | B_Window          |   409.0 μs |  5.8594 |      - |  50.31 KB |
| B_Dapper_RowNumber                        | B_Window          |   523.7 μs |  1.4648 |      - |  15.39 KB |
| B_Linq2Db_RowNumber                       | B_Window          |   782.2 μs |  6.8359 |      - |  60.94 KB |
| B_Linq2Db_SumOver                         | B_Window          |   782.8 μs |  6.8359 |      - |  58.21 KB |
