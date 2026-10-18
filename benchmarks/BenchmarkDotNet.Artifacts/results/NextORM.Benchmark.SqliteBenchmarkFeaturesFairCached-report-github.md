```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host]   : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  ShortRun : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
| Method                                    | Toolchain              | Categories        | Mean       | Gen0    | Gen1   | Allocated |
|------------------------------------------ |----------------------- |------------------ |-----------:|--------:|-------:|----------:|
| B_Nextorm_Cached_CaseWhen                 | Default                | B_CaseWhen        |   135.5 μs |  3.9063 |      - |  32.19 KB |
| B_Dapper_CaseWhen                         | Default                | B_CaseWhen        |   147.6 μs |  1.4648 |      - |  13.44 KB |
| B_Linq2Db_CaseWhen                        | Default                | B_CaseWhen        |   241.8 μs |  3.9063 |      - |  38.05 KB |
| B_EFCore_CaseWhen                         | Default                | B_CaseWhen        |   581.0 μs | 13.6719 | 5.8594 | 119.53 KB |
|                                           |                        |                   |            |         |        |           |
| B_Dapper_Cte                              | Default                | B_Cte             |   208.2 μs |  1.7090 |      - |   15.7 KB |
| B_Nextorm_Cached_Cte                      | Default                | B_Cte             |   325.1 μs | 11.7188 |      - | 108.92 KB |
| B_Linq2Db_Cte                             | Default                | B_Cte             |   557.7 μs |  9.7656 |      - |  90.63 KB |
|                                           |                        |                   |            |         |        |           |
| B_Nextorm_Cached_Distinct                 | Default                | B_Distinct        |   155.9 μs |  4.6387 |      - |  37.97 KB |
| B_Dapper_Distinct                         | Default                | B_Distinct        |   180.0 μs |  1.7090 |      - |  15.55 KB |
| B_Linq2Db_Distinct                        | Default                | B_Distinct        |   287.7 μs |  4.3945 |      - |  39.14 KB |
| B_EFCore_Distinct                         | Default                | B_Distinct        |   620.8 μs | 13.6719 | 3.9063 | 121.95 KB |
|                                           |                        |                   |            |         |        |           |
| B_Dapper_Except                           | Default                | B_Except          |   182.6 μs |  1.7090 |      - |  14.38 KB |
| B_Nextorm_Cached_Except                   | Default                | B_Except          |   185.6 μs |  7.5684 |      - |  63.75 KB |
| B_Linq2Db_Except                          | Default                | B_Except          |   375.8 μs |  5.8594 |      - |  50.55 KB |
| B_EFCore_Except                           | Default                | B_Except          |   732.8 μs | 17.5781 | 5.8594 | 153.05 KB |
|                                           |                        |                   |            |         |        |           |
| B_Dapper_In                               | Default                | B_InList          |   188.2 μs |  1.4648 |      - |  12.66 KB |
| B_Nextorm_Cached_In_AtIn_Inline           | Default                | B_InList          |   329.2 μs |  7.8125 |      - |  77.58 KB |
| B_Nextorm_Cached_In_AtIn_Captured         | Default                | B_InList          |   336.3 μs |  9.7656 |      - |  90.16 KB |
| B_Nextorm_Cached_In_ListContains_Inline   | Default                | B_InList          |   375.3 μs |  7.8125 |      - |  78.91 KB |
| B_Nextorm_Cached_In_ListContains_Captured | Default                | B_InList          |   382.2 μs |  9.7656 |      - |  92.11 KB |
| B_Linq2Db_In                              | Default                | B_InList          |   667.3 μs | 19.5313 |      - | 187.89 KB |
| B_EFCore_In                               | Default                | B_InList          | 1,218.1 μs | 27.3438 | 7.8125 | 226.11 KB |
|                                           |                        |                   |            |         |        |           |
| B_Dapper_Intersect                        | Default                | B_Intersect       |   161.8 μs |  1.4648 |      - |  12.89 KB |
| B_Nextorm_Cached_Intersect                | Default                | B_Intersect       |   172.4 μs |  7.5684 |      - |   63.6 KB |
| B_Linq2Db_Intersect                       | Default                | B_Intersect       |   399.4 μs |  5.8594 |      - |  50.08 KB |
| B_EFCore_Intersect                        | Default                | B_Intersect       |   767.5 μs | 17.5781 | 5.8594 | 143.75 KB |
|                                           |                        |                   |            |         |        |           |
| B_Dapper_Join4                            | Default                | B_Join4           |   260.9 μs |  2.4414 |      - |  20.23 KB |
| B_Nextorm_Cached_Join4                    | Default                | B_Join4           |   326.2 μs | 11.7188 |      - | 107.51 KB |
| B_Linq2Db_Join4                           | Default                | B_Join4           |   639.2 μs | 15.6250 |      - | 141.02 KB |
| B_EFCore_Join4                            | Default                | B_Join4           | 1,175.5 μs | 27.3438 | 7.8125 | 246.34 KB |
|                                           |                        |                   |            |         |        |           |
| B_Dapper_LeftJoin                         | Default                | B_LeftJoin        |   210.5 μs |  2.6855 |      - |  22.19 KB |
| B_Nextorm_Cached_LeftJoin                 | Default                | B_LeftJoin        |   226.8 μs |  6.8359 |      - |  61.64 KB |
| B_Linq2Db_LeftJoin                        | Default                | B_LeftJoin        |   817.8 μs |  7.8125 |      - |  86.02 KB |
| B_EFCore_LeftJoin                         | Default                | B_LeftJoin        | 1,028.8 μs | 23.4375 | 7.8125 | 201.34 KB |
|                                           |                        |                   |            |         |        |           |
| B_Dapper_RecursiveCte                     | Default                | B_RecursiveCte    |   254.7 μs |  1.4648 |      - |  15.31 KB |
| B_Nextorm_Cached_RecursiveCte             | Default                | B_RecursiveCte    |   369.4 μs | 15.6250 |      - | 133.13 KB |
| B_Linq2Db_RecursiveCte                    | Default                | B_RecursiveCte    |   599.7 μs |  9.7656 |      - |  83.91 KB |
|                                           |                        |                   |            |         |        |           |
| B_Dapper_ToUpper                          | Default                | B_StringFunctions |   140.3 μs |  1.4648 |      - |  12.81 KB |
| B_Nextorm_Cached_Contains                 | Default                | B_StringFunctions |   149.8 μs |  5.1270 |      - |  42.03 KB |
| B_Nextorm_Cached_ToUpper                  | Default                | B_StringFunctions |   151.1 μs |  5.1270 |      - |  42.03 KB |
| B_Dapper_Contains                         | Default                | B_StringFunctions |   188.8 μs |  1.9531 |      - |  17.19 KB |
| B_Linq2Db_Contains                        | Default                | B_StringFunctions |   297.7 μs |  5.3711 |      - |  47.81 KB |
| B_Linq2Db_ToUpper                         | Default                | B_StringFunctions |   458.2 μs |  7.8125 |      - |  63.91 KB |
| B_EFCore_ToUpper                          | Default                | B_StringFunctions |   629.2 μs | 15.6250 | 3.9063 | 132.73 KB |
| B_EFCore_Contains                         | Default                | B_StringFunctions |   676.0 μs | 15.6250 | 3.9063 | 140.08 KB |
|                                           |                        |                   |            |         |        |           |
| B_Dapper_Udf                              | Default                | B_Udf             |   142.7 μs |  1.4648 |      - |  12.81 KB |
| B_Nextorm_Cached_Udf                      | Default                | B_Udf             |   156.2 μs |  5.1270 |      - |  42.89 KB |
| B_Linq2Db_Udf                             | Default                | B_Udf             |   450.0 μs |  7.8125 |      - |     65 KB |
| B_EFCore_Udf                              | Default                | B_Udf             |   641.6 μs | 15.6250 | 3.9063 | 132.73 KB |
|                                           |                        |                   |            |         |        |           |
| B_Dapper_SumOver                          | Default                | B_Window          |   301.1 μs |  1.4648 |      - |  15.47 KB |
| B_Dapper_RowNumber                        | Default                | B_Window          |   305.3 μs |  1.4648 |      - |  15.39 KB |
| B_Nextorm_Cached_RowNumber                | Default                | B_Window          |   318.8 μs |  5.8594 |      - |  50.32 KB |
| B_Nextorm_Cached_SumOver                  | Default                | B_Window          |   330.4 μs |  5.8594 |      - |  52.27 KB |
| B_Linq2Db_SumOver                         | Default                | B_Window          |   613.0 μs |  5.8594 |      - |   58.2 KB |
| B_Linq2Db_RowNumber                       | Default                | B_Window          |   618.1 μs |  5.8594 |      - |  59.84 KB |
|                                           |                        |                   |            |         |        |           |
| B_Nextorm_Cached_CaseWhen                 | InProcessEmitToolchain | B_CaseWhen        |   141.2 μs |  3.9063 |      - |  32.19 KB |
| B_Dapper_CaseWhen                         | InProcessEmitToolchain | B_CaseWhen        |   142.3 μs |  1.4648 |      - |  13.44 KB |
| B_Linq2Db_CaseWhen                        | InProcessEmitToolchain | B_CaseWhen        |   251.4 μs |  4.3945 |      - |  38.05 KB |
| B_EFCore_CaseWhen                         | InProcessEmitToolchain | B_CaseWhen        |   601.6 μs | 13.6719 | 6.8359 | 119.54 KB |
|                                           |                        |                   |            |         |        |           |
| B_Dapper_Cte                              | InProcessEmitToolchain | B_Cte             |   212.9 μs |  1.7090 |      - |   15.7 KB |
| B_Nextorm_Cached_Cte                      | InProcessEmitToolchain | B_Cte             |   332.7 μs | 13.1836 |      - | 108.91 KB |
| B_Linq2Db_Cte                             | InProcessEmitToolchain | B_Cte             |   591.9 μs | 10.7422 |      - |  92.51 KB |
|                                           |                        |                   |            |         |        |           |
| B_Nextorm_Cached_Distinct                 | InProcessEmitToolchain | B_Distinct        |   159.5 μs |  4.6387 |      - |  37.97 KB |
| B_Dapper_Distinct                         | InProcessEmitToolchain | B_Distinct        |   187.0 μs |  1.7090 |      - |  15.55 KB |
| B_Linq2Db_Distinct                        | InProcessEmitToolchain | B_Distinct        |   311.9 μs |  4.3945 |      - |  39.14 KB |
| B_EFCore_Distinct                         | InProcessEmitToolchain | B_Distinct        |   667.7 μs | 14.6484 | 4.8828 | 121.96 KB |
|                                           |                        |                   |            |         |        |           |
| B_Nextorm_Cached_Except                   | InProcessEmitToolchain | B_Except          |   195.5 μs |  7.5684 |      - |  63.75 KB |
| B_Dapper_Except                           | InProcessEmitToolchain | B_Except          |   206.3 μs |  1.7090 |      - |  14.38 KB |
| B_Linq2Db_Except                          | InProcessEmitToolchain | B_Except          |   452.0 μs |  5.8594 |      - |  50.55 KB |
| B_EFCore_Except                           | InProcessEmitToolchain | B_Except          |   946.4 μs | 18.5547 | 5.8594 | 153.05 KB |
|                                           |                        |                   |            |         |        |           |
| B_Dapper_In                               | InProcessEmitToolchain | B_InList          |   186.0 μs |  1.4648 |      - |  12.66 KB |
| B_Nextorm_Cached_In_AtIn_Inline           | InProcessEmitToolchain | B_InList          |   344.2 μs |  9.2773 |      - |  77.58 KB |
| B_Nextorm_Cached_In_AtIn_Captured         | InProcessEmitToolchain | B_InList          |   344.9 μs | 10.7422 |      - |  90.16 KB |
| B_Nextorm_Cached_In_ListContains_Inline   | InProcessEmitToolchain | B_InList          |   357.9 μs |  9.2773 |      - |  78.91 KB |
| B_Nextorm_Cached_In_ListContains_Captured | InProcessEmitToolchain | B_InList          |   367.4 μs | 11.2305 |      - |  92.11 KB |
| B_Linq2Db_In                              | InProcessEmitToolchain | B_InList          |   728.4 μs | 22.4609 |      - | 190.55 KB |
| B_EFCore_In                               | InProcessEmitToolchain | B_InList          | 1,087.0 μs | 27.3438 | 7.8125 | 226.12 KB |
|                                           |                        |                   |            |         |        |           |
| B_Nextorm_Cached_Intersect                | InProcessEmitToolchain | B_Intersect       |   171.4 μs |  7.5684 |      - |  63.59 KB |
| B_Dapper_Intersect                        | InProcessEmitToolchain | B_Intersect       |   176.1 μs |  1.4648 |      - |  12.89 KB |
| B_Linq2Db_Intersect                       | InProcessEmitToolchain | B_Intersect       |   421.8 μs |  5.8594 |      - |  51.56 KB |
| B_EFCore_Intersect                        | InProcessEmitToolchain | B_Intersect       |   820.8 μs | 17.5781 | 5.8594 | 143.75 KB |
|                                           |                        |                   |            |         |        |           |
| B_Dapper_Join4                            | InProcessEmitToolchain | B_Join4           |   265.4 μs |  2.4414 |      - |  20.24 KB |
| B_Nextorm_Cached_Join4                    | InProcessEmitToolchain | B_Join4           |   347.4 μs | 13.1836 |      - | 108.29 KB |
| B_Linq2Db_Join4                           | InProcessEmitToolchain | B_Join4           |   688.5 μs | 16.6016 |      - | 143.29 KB |
| B_EFCore_Join4                            | InProcessEmitToolchain | B_Join4           | 1,202.8 μs | 29.2969 | 7.8125 | 247.75 KB |
|                                           |                        |                   |            |         |        |           |
| B_Dapper_LeftJoin                         | InProcessEmitToolchain | B_LeftJoin        |   207.7 μs |  2.6855 |      - |  22.19 KB |
| B_Nextorm_Cached_LeftJoin                 | InProcessEmitToolchain | B_LeftJoin        |   239.8 μs |  7.3242 |      - |  61.64 KB |
| B_Linq2Db_LeftJoin                        | InProcessEmitToolchain | B_LeftJoin        |   742.4 μs |  9.7656 |      - |   86.8 KB |
| B_EFCore_LeftJoin                         | InProcessEmitToolchain | B_LeftJoin        |   987.3 μs | 23.4375 | 7.8125 | 201.97 KB |
|                                           |                        |                   |            |         |        |           |
| B_Dapper_RecursiveCte                     | InProcessEmitToolchain | B_RecursiveCte    |   252.1 μs |  1.4648 |      - |  15.31 KB |
| B_Nextorm_Cached_RecursiveCte             | InProcessEmitToolchain | B_RecursiveCte    |   387.4 μs | 16.1133 |      - | 134.53 KB |
| B_Linq2Db_RecursiveCte                    | InProcessEmitToolchain | B_RecursiveCte    |   626.7 μs |  9.7656 |      - |  83.91 KB |
|                                           |                        |                   |            |         |        |           |
| B_Dapper_ToUpper                          | InProcessEmitToolchain | B_StringFunctions |   147.0 μs |  1.4648 |      - |  12.81 KB |
| B_Nextorm_Cached_Contains                 | InProcessEmitToolchain | B_StringFunctions |   159.4 μs |  5.1270 |      - |  42.03 KB |
| B_Nextorm_Cached_ToUpper                  | InProcessEmitToolchain | B_StringFunctions |   159.7 μs |  5.1270 |      - |  42.03 KB |
| B_Dapper_Contains                         | InProcessEmitToolchain | B_StringFunctions |   197.7 μs |  1.9531 |      - |  17.19 KB |
| B_Linq2Db_Contains                        | InProcessEmitToolchain | B_StringFunctions |   326.4 μs |  5.3711 |      - |  47.81 KB |
| B_Linq2Db_ToUpper                         | InProcessEmitToolchain | B_StringFunctions |   514.2 μs |  7.8125 |      - |  65.08 KB |
| B_EFCore_ToUpper                          | InProcessEmitToolchain | B_StringFunctions |   691.3 μs | 15.6250 | 4.8828 | 132.74 KB |
| B_EFCore_Contains                         | InProcessEmitToolchain | B_StringFunctions |   699.6 μs | 16.6016 | 4.8828 | 140.09 KB |
|                                           |                        |                   |            |         |        |           |
| B_Dapper_Udf                              | InProcessEmitToolchain | B_Udf             |   146.3 μs |  1.4648 |      - |  12.81 KB |
| B_Nextorm_Cached_Udf                      | InProcessEmitToolchain | B_Udf             |   161.1 μs |  5.1270 |      - |  42.89 KB |
| B_Linq2Db_Udf                             | InProcessEmitToolchain | B_Udf             |   516.1 μs |  7.8125 |      - |  65.08 KB |
| B_EFCore_Udf                              | InProcessEmitToolchain | B_Udf             |   691.4 μs | 15.6250 | 4.8828 | 132.74 KB |
|                                           |                        |                   |            |         |        |           |
| B_Dapper_SumOver                          | InProcessEmitToolchain | B_Window          |   313.0 μs |  1.4648 |      - |  15.47 KB |
| B_Dapper_RowNumber                        | InProcessEmitToolchain | B_Window          |   314.5 μs |  1.4648 |      - |  15.39 KB |
| B_Nextorm_Cached_RowNumber                | InProcessEmitToolchain | B_Window          |   347.3 μs |  5.8594 |      - |  51.56 KB |
| B_Nextorm_Cached_SumOver                  | InProcessEmitToolchain | B_Window          |   352.9 μs |  6.3477 |      - |  53.52 KB |
| B_Linq2Db_SumOver                         | InProcessEmitToolchain | B_Window          |   653.2 μs |  6.8359 |      - |  58.21 KB |
| B_Linq2Db_RowNumber                       | InProcessEmitToolchain | B_Window          |   660.9 μs |  6.8359 |      - |  59.85 KB |
