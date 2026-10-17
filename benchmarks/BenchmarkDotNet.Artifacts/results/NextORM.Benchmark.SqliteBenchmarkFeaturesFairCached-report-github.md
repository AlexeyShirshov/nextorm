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
| B_Nextorm_Cached_CaseWhen                 | Default                | B_CaseWhen        |   131.0 μs |  3.9063 |      - |  32.19 KB |
| B_Dapper_CaseWhen                         | Default                | B_CaseWhen        |   142.6 μs |  1.4648 |      - |  13.44 KB |
| B_Linq2Db_CaseWhen                        | Default                | B_CaseWhen        |   235.7 μs |  4.6387 |      - |  38.05 KB |
| B_EFCore_CaseWhen                         | Default                | B_CaseWhen        |   589.4 μs | 13.6719 | 5.8594 | 119.53 KB |
|                                           |                        |                   |            |         |        |           |
| B_Dapper_Cte                              | Default                | B_Cte             |   218.4 μs |  1.7090 |      - |   15.7 KB |
| B_Nextorm_Cached_Cte                      | Default                | B_Cte             |   320.0 μs | 11.7188 |      - | 110.32 KB |
| B_Linq2Db_Cte                             | Default                | B_Cte             |   539.2 μs |  9.7656 |      - |  90.63 KB |
|                                           |                        |                   |            |         |        |           |
| B_Nextorm_Cached_Distinct                 | Default                | B_Distinct        |   157.2 μs |  4.6387 |      - |  37.97 KB |
| B_Dapper_Distinct                         | Default                | B_Distinct        |   181.5 μs |  1.7090 |      - |  15.55 KB |
| B_Linq2Db_Distinct                        | Default                | B_Distinct        |   295.5 μs |  4.3945 |      - |  39.14 KB |
| B_EFCore_Distinct                         | Default                | B_Distinct        |   661.0 μs | 13.6719 | 3.9063 | 121.95 KB |
|                                           |                        |                   |            |         |        |           |
| B_Dapper_Except                           | Default                | B_Except          |   178.0 μs |  1.7090 |      - |  14.38 KB |
| B_Nextorm_Cached_Except                   | Default                | B_Except          |   180.1 μs |  7.5684 |      - |  63.75 KB |
| B_Linq2Db_Except                          | Default                | B_Except          |   364.2 μs |  5.8594 |      - |  50.55 KB |
| B_EFCore_Except                           | Default                | B_Except          |   723.5 μs | 17.5781 | 5.8594 | 153.05 KB |
|                                           |                        |                   |            |         |        |           |
| B_Dapper_In                               | Default                | B_InList          |   184.7 μs |  1.4648 |      - |  12.66 KB |
| B_Nextorm_Cached_In_AtIn_Inline           | Default                | B_InList          |   289.2 μs |  7.8125 |      - |  77.58 KB |
| B_Nextorm_Cached_In_AtIn_Captured         | Default                | B_InList          |   305.2 μs | 10.7422 |      - |  90.16 KB |
| B_Nextorm_Cached_In_ListContains_Inline   | Default                | B_InList          |   314.5 μs |  7.8125 |      - |  78.91 KB |
| B_Nextorm_Cached_In_ListContains_Captured | Default                | B_InList          |   324.6 μs |  9.7656 |      - |  92.11 KB |
| B_Linq2Db_In                              | Default                | B_InList          |   609.7 μs | 22.4609 |      - | 187.89 KB |
| B_EFCore_In                               | Default                | B_InList          | 1,129.0 μs | 27.3438 | 7.8125 | 226.11 KB |
|                                           |                        |                   |            |         |        |           |
| B_Dapper_Intersect                        | Default                | B_Intersect       |   162.4 μs |  1.4648 |      - |  12.89 KB |
| B_Nextorm_Cached_Intersect                | Default                | B_Intersect       |   171.5 μs |  7.5684 |      - |   63.6 KB |
| B_Linq2Db_Intersect                       | Default                | B_Intersect       |   340.9 μs |  5.8594 |      - |  50.08 KB |
| B_EFCore_Intersect                        | Default                | B_Intersect       |   692.2 μs | 17.5781 | 5.8594 | 143.75 KB |
|                                           |                        |                   |            |         |        |           |
| B_Dapper_Join4                            | Default                | B_Join4           |   253.5 μs |  2.4414 |      - |  20.23 KB |
| B_Nextorm_Cached_Join4                    | Default                | B_Join4           |   321.2 μs | 11.7188 |      - | 108.29 KB |
| B_Linq2Db_Join4                           | Default                | B_Join4           |   606.7 μs | 15.6250 |      - | 138.68 KB |
| B_EFCore_Join4                            | Default                | B_Join4           | 1,140.1 μs | 27.3438 | 7.8125 | 246.49 KB |
|                                           |                        |                   |            |         |        |           |
| B_Dapper_LeftJoin                         | Default                | B_LeftJoin        |   202.6 μs |  2.6855 |      - |  22.19 KB |
| B_Nextorm_Cached_LeftJoin                 | Default                | B_LeftJoin        |   227.1 μs |  6.8359 |      - |  62.43 KB |
| B_Linq2Db_LeftJoin                        | Default                | B_LeftJoin        |   716.2 μs |  7.8125 |      - |  86.02 KB |
| B_EFCore_LeftJoin                         | Default                | B_LeftJoin        |   970.1 μs | 23.4375 | 7.8125 | 201.34 KB |
|                                           |                        |                   |            |         |        |           |
| B_Dapper_RecursiveCte                     | Default                | B_RecursiveCte    |   242.0 μs |  1.7090 |      - |  15.31 KB |
| B_Nextorm_Cached_RecursiveCte             | Default                | B_RecursiveCte    |   343.3 μs | 15.6250 |      - | 133.13 KB |
| B_Linq2Db_RecursiveCte                    | Default                | B_RecursiveCte    |   586.2 μs |  9.7656 |      - |  85.31 KB |
|                                           |                        |                   |            |         |        |           |
| B_Dapper_ToUpper                          | Default                | B_StringFunctions |   141.3 μs |  1.4648 |      - |  12.81 KB |
| B_Nextorm_Cached_ToUpper                  | Default                | B_StringFunctions |   145.8 μs |  5.1270 |      - |  42.03 KB |
| B_Nextorm_Cached_Contains                 | Default                | B_StringFunctions |   147.8 μs |  5.1270 |      - |  42.03 KB |
| B_Dapper_Contains                         | Default                | B_StringFunctions |   187.6 μs |  1.9531 |      - |  17.19 KB |
| B_Linq2Db_Contains                        | Default                | B_StringFunctions |   290.4 μs |  5.3711 |      - |  47.81 KB |
| B_Linq2Db_ToUpper                         | Default                | B_StringFunctions |   424.9 μs |  7.8125 |      - |  63.91 KB |
| B_EFCore_ToUpper                          | Default                | B_StringFunctions |   617.5 μs | 15.6250 | 3.9063 | 132.73 KB |
| B_EFCore_Contains                         | Default                | B_StringFunctions |   674.6 μs | 15.6250 | 3.9063 | 140.08 KB |
|                                           |                        |                   |            |         |        |           |
| B_Dapper_Udf                              | Default                | B_Udf             |   141.8 μs |  1.4648 |      - |  12.81 KB |
| B_Nextorm_Cached_Udf                      | Default                | B_Udf             |   149.6 μs |  5.1270 |      - |  42.89 KB |
| B_Linq2Db_Udf                             | Default                | B_Udf             |   445.3 μs |  7.8125 |      - |  63.91 KB |
| B_EFCore_Udf                              | Default                | B_Udf             |   626.6 μs | 15.6250 | 3.9063 | 133.83 KB |
|                                           |                        |                   |            |         |        |           |
| B_Dapper_RowNumber                        | Default                | B_Window          |   299.7 μs |  1.4648 |      - |  15.39 KB |
| B_Dapper_SumOver                          | Default                | B_Window          |   300.3 μs |  1.4648 |      - |  15.47 KB |
| B_Nextorm_Cached_SumOver                  | Default                | B_Window          |   323.6 μs |  5.8594 |      - |  53.36 KB |
| B_Nextorm_Cached_RowNumber                | Default                | B_Window          |   324.8 μs |  5.8594 |      - |  50.32 KB |
| B_Linq2Db_RowNumber                       | Default                | B_Window          |   591.1 μs |  6.8359 |      - |  61.33 KB |
| B_Linq2Db_SumOver                         | Default                | B_Window          |   597.9 μs |  5.8594 |      - |   59.3 KB |
|                                           |                        |                   |            |         |        |           |
| B_Nextorm_Cached_CaseWhen                 | InProcessEmitToolchain | B_CaseWhen        |   138.3 μs |  3.9063 |      - |  32.19 KB |
| B_Dapper_CaseWhen                         | InProcessEmitToolchain | B_CaseWhen        |   140.9 μs |  1.4648 |      - |  13.44 KB |
| B_Linq2Db_CaseWhen                        | InProcessEmitToolchain | B_CaseWhen        |   255.8 μs |  4.3945 |      - |  38.05 KB |
| B_EFCore_CaseWhen                         | InProcessEmitToolchain | B_CaseWhen        |   584.3 μs | 13.6719 | 6.8359 | 119.54 KB |
|                                           |                        |                   |            |         |        |           |
| B_Dapper_Cte                              | InProcessEmitToolchain | B_Cte             |   208.8 μs |  1.7090 |      - |   15.7 KB |
| B_Nextorm_Cached_Cte                      | InProcessEmitToolchain | B_Cte             |   335.2 μs | 13.1836 |      - | 110.48 KB |
| B_Linq2Db_Cte                             | InProcessEmitToolchain | B_Cte             |   601.1 μs | 10.7422 |      - |  92.58 KB |
|                                           |                        |                   |            |         |        |           |
| B_Nextorm_Cached_Distinct                 | InProcessEmitToolchain | B_Distinct        |   155.2 μs |  4.6387 |      - |  37.97 KB |
| B_Dapper_Distinct                         | InProcessEmitToolchain | B_Distinct        |   181.1 μs |  1.7090 |      - |  15.55 KB |
| B_Linq2Db_Distinct                        | InProcessEmitToolchain | B_Distinct        |   306.2 μs |  4.3945 |      - |  39.14 KB |
| B_EFCore_Distinct                         | InProcessEmitToolchain | B_Distinct        |   644.1 μs | 14.6484 | 4.8828 | 121.96 KB |
|                                           |                        |                   |            |         |        |           |
| B_Dapper_Except                           | InProcessEmitToolchain | B_Except          |   172.5 μs |  1.7090 |      - |  14.38 KB |
| B_Nextorm_Cached_Except                   | InProcessEmitToolchain | B_Except          |   181.2 μs |  7.5684 |      - |  63.75 KB |
| B_Linq2Db_Except                          | InProcessEmitToolchain | B_Except          |   410.7 μs |  5.8594 |      - |  50.55 KB |
| B_EFCore_Except                           | InProcessEmitToolchain | B_Except          |   744.1 μs | 18.5547 | 5.8594 | 153.05 KB |
|                                           |                        |                   |            |         |        |           |
| B_Dapper_In                               | InProcessEmitToolchain | B_InList          |   177.8 μs |  1.4648 |      - |  12.66 KB |
| B_Nextorm_Cached_In_AtIn_Inline           | InProcessEmitToolchain | B_InList          |   326.4 μs |  9.2773 |      - |  77.58 KB |
| B_Nextorm_Cached_In_AtIn_Captured         | InProcessEmitToolchain | B_InList          |   331.9 μs | 10.7422 |      - |  90.16 KB |
| B_Nextorm_Cached_In_ListContains_Inline   | InProcessEmitToolchain | B_InList          |   341.4 μs |  9.2773 |      - |  78.91 KB |
| B_Nextorm_Cached_In_ListContains_Captured | InProcessEmitToolchain | B_InList          |   344.4 μs | 11.2305 |      - |  92.11 KB |
| B_Linq2Db_In                              | InProcessEmitToolchain | B_InList          |   712.3 μs | 22.4609 |      - | 189.15 KB |
| B_EFCore_In                               | InProcessEmitToolchain | B_InList          | 1,060.0 μs | 27.3438 | 7.8125 | 226.12 KB |
|                                           |                        |                   |            |         |        |           |
| B_Dapper_Intersect                        | InProcessEmitToolchain | B_Intersect       |   161.2 μs |  1.4648 |      - |  12.89 KB |
| B_Nextorm_Cached_Intersect                | InProcessEmitToolchain | B_Intersect       |   172.0 μs |  7.5684 |      - |  63.59 KB |
| B_Linq2Db_Intersect                       | InProcessEmitToolchain | B_Intersect       |   366.0 μs |  5.8594 |      - |  51.64 KB |
| B_EFCore_Intersect                        | InProcessEmitToolchain | B_Intersect       |   722.8 μs | 17.5781 | 5.8594 | 143.75 KB |
|                                           |                        |                   |            |         |        |           |
| B_Dapper_Join4                            | InProcessEmitToolchain | B_Join4           |   256.9 μs |  2.4414 |      - |  20.24 KB |
| B_Nextorm_Cached_Join4                    | InProcessEmitToolchain | B_Join4           |   365.4 μs | 13.1836 |      - | 109.07 KB |
| B_Linq2Db_Join4                           | InProcessEmitToolchain | B_Join4           |   673.9 μs | 16.6016 |      - | 140.64 KB |
| B_EFCore_Join4                            | InProcessEmitToolchain | B_Join4           | 1,200.2 μs | 29.2969 | 7.8125 | 244.79 KB |
|                                           |                        |                   |            |         |        |           |
| B_Dapper_LeftJoin                         | InProcessEmitToolchain | B_LeftJoin        |   199.2 μs |  2.6855 |      - |  22.19 KB |
| B_Nextorm_Cached_LeftJoin                 | InProcessEmitToolchain | B_LeftJoin        |   241.3 μs |  7.3242 |      - |  61.64 KB |
| B_Linq2Db_LeftJoin                        | InProcessEmitToolchain | B_LeftJoin        |   732.7 μs |  9.7656 |      - |  86.02 KB |
| B_EFCore_LeftJoin                         | InProcessEmitToolchain | B_LeftJoin        |   988.6 μs | 23.4375 | 7.8125 | 203.06 KB |
|                                           |                        |                   |            |         |        |           |
| B_Dapper_RecursiveCte                     | InProcessEmitToolchain | B_RecursiveCte    |   244.9 μs |  1.7090 |      - |  15.31 KB |
| B_Nextorm_Cached_RecursiveCte             | InProcessEmitToolchain | B_RecursiveCte    |   370.2 μs | 16.1133 |      - | 133.13 KB |
| B_Linq2Db_RecursiveCte                    | InProcessEmitToolchain | B_RecursiveCte    |   625.1 μs |  9.7656 |      - |  83.91 KB |
|                                           |                        |                   |            |         |        |           |
| B_Dapper_ToUpper                          | InProcessEmitToolchain | B_StringFunctions |   142.4 μs |  1.4648 |      - |  12.81 KB |
| B_Nextorm_Cached_Contains                 | InProcessEmitToolchain | B_StringFunctions |   152.8 μs |  5.1270 |      - |  42.03 KB |
| B_Nextorm_Cached_ToUpper                  | InProcessEmitToolchain | B_StringFunctions |   156.3 μs |  5.1270 |      - |  42.03 KB |
| B_Dapper_Contains                         | InProcessEmitToolchain | B_StringFunctions |   196.2 μs |  1.9531 |      - |  17.19 KB |
| B_Linq2Db_Contains                        | InProcessEmitToolchain | B_StringFunctions |   310.4 μs |  5.3711 |      - |  47.81 KB |
| B_Linq2Db_ToUpper                         | InProcessEmitToolchain | B_StringFunctions |   484.5 μs |  7.8125 |      - |  63.91 KB |
| B_EFCore_ToUpper                          | InProcessEmitToolchain | B_StringFunctions |   660.5 μs | 15.6250 | 4.8828 | 132.74 KB |
| B_EFCore_Contains                         | InProcessEmitToolchain | B_StringFunctions |   686.0 μs | 16.6016 | 4.8828 | 140.09 KB |
|                                           |                        |                   |            |         |        |           |
| B_Dapper_Udf                              | InProcessEmitToolchain | B_Udf             |   140.7 μs |  1.4648 |      - |  12.81 KB |
| B_Nextorm_Cached_Udf                      | InProcessEmitToolchain | B_Udf             |   157.9 μs |  5.1270 |      - |  42.89 KB |
| B_Linq2Db_Udf                             | InProcessEmitToolchain | B_Udf             |   493.2 μs |  7.8125 |      - |  63.91 KB |
| B_EFCore_Udf                              | InProcessEmitToolchain | B_Udf             |   673.4 μs | 15.6250 | 4.8828 | 132.74 KB |
|                                           |                        |                   |            |         |        |           |
| B_Dapper_SumOver                          | InProcessEmitToolchain | B_Window          |   294.9 μs |  1.4648 |      - |  15.47 KB |
| B_Dapper_RowNumber                        | InProcessEmitToolchain | B_Window          |   296.4 μs |  1.4648 |      - |  15.39 KB |
| B_Nextorm_Cached_SumOver                  | InProcessEmitToolchain | B_Window          |   332.2 μs |  6.3477 |      - |  53.52 KB |
| B_Nextorm_Cached_RowNumber                | InProcessEmitToolchain | B_Window          |   336.4 μs |  5.8594 |      - |  50.31 KB |
| B_Linq2Db_SumOver                         | InProcessEmitToolchain | B_Window          |   632.3 μs |  6.8359 |      - |  58.21 KB |
| B_Linq2Db_RowNumber                       | InProcessEmitToolchain | B_Window          |   749.4 μs |  6.8359 |      - |  60.63 KB |
