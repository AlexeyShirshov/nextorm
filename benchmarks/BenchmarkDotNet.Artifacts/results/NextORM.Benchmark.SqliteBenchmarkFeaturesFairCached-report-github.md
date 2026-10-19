```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host]   : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  ShortRun : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
| Method                                    | Toolchain              | Categories        | Mean       | Median     | Gen0    | Gen1   | Allocated |
|------------------------------------------ |----------------------- |------------------ |-----------:|-----------:|--------:|-------:|----------:|
| B_Dapper_CaseWhen                         | Default                | B_CaseWhen        |   149.1 μs |   148.6 μs |  1.4648 |      - |  13.44 KB |
| B_Nextorm_Cached_CaseWhen                 | Default                | B_CaseWhen        |   154.8 μs |   150.8 μs |  3.9063 |      - |  32.19 KB |
| B_Linq2Db_CaseWhen                        | Default                | B_CaseWhen        |   261.4 μs |   249.9 μs |  3.9063 |      - |  38.05 KB |
| B_EFCore_CaseWhen                         | Default                | B_CaseWhen        |   608.9 μs |   619.4 μs | 13.6719 | 5.8594 | 119.53 KB |
|                                           |                        |                   |            |            |         |        |           |
| B_Dapper_Cte                              | Default                | B_Cte             |   222.5 μs |   220.7 μs |  1.7090 |      - |   15.7 KB |
| B_Nextorm_Cached_Cte                      | Default                | B_Cte             |   340.8 μs |   334.8 μs | 11.7188 |      - | 108.92 KB |
| B_Linq2Db_Cte                             | Default                | B_Cte             |   585.2 μs |   578.6 μs |  9.7656 |      - |   91.8 KB |
|                                           |                        |                   |            |            |         |        |           |
| B_Nextorm_Cached_Distinct                 | Default                | B_Distinct        |   165.2 μs |   165.1 μs |  4.6387 |      - |  37.97 KB |
| B_Dapper_Distinct                         | Default                | B_Distinct        |   188.3 μs |   185.9 μs |  1.7090 |      - |  15.55 KB |
| B_Linq2Db_Distinct                        | Default                | B_Distinct        |   297.6 μs |   302.5 μs |  4.3945 |      - |  39.14 KB |
| B_EFCore_Distinct                         | Default                | B_Distinct        |   676.2 μs |   683.3 μs | 13.6719 | 3.9063 | 121.95 KB |
|                                           |                        |                   |            |            |         |        |           |
| B_Dapper_Except                           | Default                | B_Except          |   181.9 μs |   179.2 μs |  1.7090 |      - |  14.38 KB |
| B_Nextorm_Cached_Except                   | Default                | B_Except          |   387.0 μs |   393.3 μs |  7.5684 |      - |  63.75 KB |
| B_Linq2Db_Except                          | Default                | B_Except          |   467.2 μs |   486.3 μs |  5.8594 |      - |  50.55 KB |
| B_EFCore_Except                           | Default                | B_Except          | 2,658.1 μs | 2,537.8 μs | 15.6250 |      - | 138.52 KB |
|                                           |                        |                   |            |            |         |        |           |
| B_Dapper_In                               | Default                | B_InList          |   209.7 μs |   205.2 μs |  1.4648 |      - |  12.66 KB |
| B_Nextorm_Cached_In_AtIn_Inline           | Default                | B_InList          |   321.6 μs |   321.1 μs |  7.8125 |      - |  77.58 KB |
| B_Nextorm_Cached_In_AtIn_Captured         | Default                | B_InList          |   327.0 μs |   328.1 μs |  9.7656 |      - |  90.16 KB |
| B_Nextorm_Cached_In_ListContains_Inline   | Default                | B_InList          |   330.5 μs |   334.8 μs |  7.8125 |      - |  78.91 KB |
| B_Nextorm_Cached_In_ListContains_Captured | Default                | B_InList          |   340.8 μs |   337.7 μs |  9.7656 |      - |  92.11 KB |
| B_Linq2Db_In                              | Default                | B_InList          |   708.0 μs |   687.5 μs | 19.5313 |      - | 187.89 KB |
| B_EFCore_In                               | Default                | B_InList          | 1,295.5 μs | 1,148.6 μs | 27.3438 | 7.8125 | 226.11 KB |
|                                           |                        |                   |            |            |         |        |           |
| B_Dapper_Intersect                        | Default                | B_Intersect       |   165.3 μs |   163.2 μs |  1.4648 |      - |  12.89 KB |
| B_Nextorm_Cached_Intersect                | Default                | B_Intersect       |   179.8 μs |   180.4 μs |  7.5684 |      - |   63.6 KB |
| B_Linq2Db_Intersect                       | Default                | B_Intersect       |   350.7 μs |   346.0 μs |  5.8594 |      - |  50.08 KB |
| B_EFCore_Intersect                        | Default                | B_Intersect       |   732.9 μs |   734.1 μs | 17.5781 | 5.8594 | 143.75 KB |
|                                           |                        |                   |            |            |         |        |           |
| B_Dapper_Join4                            | Default                | B_Join4           |   262.0 μs |   261.2 μs |  2.4414 |      - |  20.23 KB |
| B_Nextorm_Cached_Join4                    | Default                | B_Join4           |   334.5 μs |   332.9 μs | 11.7188 |      - | 106.73 KB |
| B_Linq2Db_Join4                           | Default                | B_Join4           |   652.2 μs |   652.2 μs | 15.6250 |      - | 142.74 KB |
| B_EFCore_Join4                            | Default                | B_Join4           | 1,173.5 μs | 1,138.8 μs | 27.3438 | 7.8125 | 245.56 KB |
|                                           |                        |                   |            |            |         |        |           |
| B_Dapper_LeftJoin                         | Default                | B_LeftJoin        |   209.8 μs |   207.2 μs |  2.6855 |      - |  22.19 KB |
| B_Nextorm_Cached_LeftJoin                 | Default                | B_LeftJoin        |   278.4 μs |   287.3 μs |  5.8594 |      - |  61.64 KB |
| B_Linq2Db_LeftJoin                        | Default                | B_LeftJoin        |   735.4 μs |   703.5 μs |  7.8125 |      - |  86.02 KB |
| B_EFCore_LeftJoin                         | Default                | B_LeftJoin        |   980.9 μs |   982.3 μs | 23.4375 | 7.8125 | 202.82 KB |
|                                           |                        |                   |            |            |         |        |           |
| B_Dapper_RecursiveCte                     | Default                | B_RecursiveCte    |   249.2 μs |   248.1 μs |  1.4648 |      - |  15.31 KB |
| B_Nextorm_Cached_RecursiveCte             | Default                | B_RecursiveCte    |   368.5 μs |   368.2 μs | 15.6250 |      - | 133.13 KB |
| B_Linq2Db_RecursiveCte                    | Default                | B_RecursiveCte    |   570.2 μs |   570.8 μs |  9.7656 |      - |  83.91 KB |
|                                           |                        |                   |            |            |         |        |           |
| B_Nextorm_Cached_ToUpper                  | Default                | B_StringFunctions |   152.5 μs |   152.4 μs |  5.1270 |      - |  42.03 KB |
| B_Dapper_ToUpper                          | Default                | B_StringFunctions |   203.6 μs |   165.5 μs |  1.4648 |      - |  12.81 KB |
| B_EFCore_ToUpper                          | Default                | B_StringFunctions |   672.5 μs |   658.0 μs | 15.6250 | 3.9063 | 132.73 KB |
| B_Nextorm_Cached_Contains                 | Default                | B_StringFunctions |   770.4 μs |   758.9 μs |  3.9063 |      - |  42.03 KB |
| B_EFCore_Contains                         | Default                | B_StringFunctions | 1,320.7 μs | 1,319.3 μs | 15.6250 | 3.9063 | 140.08 KB |
| B_Dapper_Contains                         | Default                | B_StringFunctions | 1,429.7 μs | 1,394.0 μs |  1.9531 |      - |  17.19 KB |
| B_Linq2Db_Contains                        | Default                | B_StringFunctions | 1,715.6 μs | 1,868.2 μs |  3.9063 |      - |  47.81 KB |
| B_Linq2Db_ToUpper                         | Default                | B_StringFunctions | 1,745.2 μs | 1,761.1 μs |  7.8125 |      - |  63.91 KB |
|                                           |                        |                   |            |            |         |        |           |
| B_Dapper_Udf                              | Default                | B_Udf             |   174.6 μs |   176.7 μs |  1.4648 |      - |  12.81 KB |
| B_Nextorm_Cached_Udf                      | Default                | B_Udf             |   182.1 μs |   180.5 μs |  4.8828 |      - |  42.89 KB |
| B_Linq2Db_Udf                             | Default                | B_Udf             |   685.5 μs |   691.1 μs |  7.8125 |      - |  64.36 KB |
| B_EFCore_Udf                              | Default                | B_Udf             | 1,906.1 μs | 1,576.1 μs |  7.8125 |      - | 124.14 KB |
|                                           |                        |                   |            |            |         |        |           |
| B_Dapper_SumOver                          | Default                | B_Window          |   348.8 μs |   344.1 μs |  1.4648 |      - |  15.47 KB |
| B_Dapper_RowNumber                        | Default                | B_Window          |   358.0 μs |   345.6 μs |  1.4648 |      - |  15.39 KB |
| B_Nextorm_Cached_RowNumber                | Default                | B_Window          |   384.4 μs |   368.5 μs |  5.8594 |      - |  50.32 KB |
| B_Nextorm_Cached_SumOver                  | Default                | B_Window          |   393.1 μs |   404.1 μs |  5.8594 |      - |  52.27 KB |
| B_Linq2Db_SumOver                         | Default                | B_Window          |   671.3 μs |   665.5 μs |  5.8594 |      - |   58.2 KB |
| B_Linq2Db_RowNumber                       | Default                | B_Window          |   695.4 μs |   670.8 μs |  5.8594 |      - |  61.02 KB |
|                                           |                        |                   |            |            |         |        |           |
| B_Nextorm_Cached_CaseWhen                 | InProcessEmitToolchain | B_CaseWhen        |   144.9 μs |   140.1 μs |  3.9063 |      - |  32.19 KB |
| B_Dapper_CaseWhen                         | InProcessEmitToolchain | B_CaseWhen        |   149.5 μs |   149.8 μs |  1.4648 |      - |  13.44 KB |
| B_Linq2Db_CaseWhen                        | InProcessEmitToolchain | B_CaseWhen        |   274.1 μs |   268.3 μs |  4.3945 |      - |  38.05 KB |
| B_EFCore_CaseWhen                         | InProcessEmitToolchain | B_CaseWhen        |   593.5 μs |   593.7 μs | 13.6719 | 6.8359 | 119.54 KB |
|                                           |                        |                   |            |            |         |        |           |
| B_Nextorm_Cached_Cte                      | InProcessEmitToolchain | B_Cte             |   343.7 μs |   335.5 μs | 13.1836 |      - | 108.91 KB |
| B_Dapper_Cte                              | InProcessEmitToolchain | B_Cte             |   467.2 μs |   453.7 μs |  1.4648 |      - |  15.71 KB |
| B_Linq2Db_Cte                             | InProcessEmitToolchain | B_Cte             | 1,193.6 μs | 1,218.2 μs | 10.7422 |      - |  90.63 KB |
|                                           |                        |                   |            |            |         |        |           |
| B_Nextorm_Cached_Distinct                 | InProcessEmitToolchain | B_Distinct        |   184.3 μs |   183.2 μs |  4.6387 |      - |  37.97 KB |
| B_Dapper_Distinct                         | InProcessEmitToolchain | B_Distinct        |   259.4 μs |   266.3 μs |  1.4648 |      - |  15.55 KB |
| B_Linq2Db_Distinct                        | InProcessEmitToolchain | B_Distinct        |   667.4 μs |   675.4 μs |  4.3945 |      - |  39.14 KB |
| B_EFCore_Distinct                         | InProcessEmitToolchain | B_Distinct        |   767.5 μs |   770.5 μs | 14.6484 | 4.8828 | 121.96 KB |
|                                           |                        |                   |            |            |         |        |           |
| B_Nextorm_Cached_Except                   | InProcessEmitToolchain | B_Except          |   191.7 μs |   190.2 μs |  7.5684 |      - |  63.75 KB |
| B_Dapper_Except                           | InProcessEmitToolchain | B_Except          |   210.8 μs |   206.0 μs |  1.7090 |      - |  14.38 KB |
| B_Linq2Db_Except                          | InProcessEmitToolchain | B_Except          |   412.3 μs |   408.6 μs |  5.8594 |      - |  50.55 KB |
| B_EFCore_Except                           | InProcessEmitToolchain | B_Except          |   860.9 μs |   858.5 μs | 18.5547 | 5.8594 | 153.05 KB |
|                                           |                        |                   |            |            |         |        |           |
| B_Dapper_In                               | InProcessEmitToolchain | B_InList          |   196.5 μs |   193.3 μs |  1.4648 |      - |  12.66 KB |
| B_Nextorm_Cached_In_AtIn_Inline           | InProcessEmitToolchain | B_InList          |   342.9 μs |   342.2 μs |  9.2773 |      - |  78.75 KB |
| B_Nextorm_Cached_In_AtIn_Captured         | InProcessEmitToolchain | B_InList          |   350.7 μs |   352.2 μs | 10.7422 |      - |  91.33 KB |
| B_Nextorm_Cached_In_ListContains_Inline   | InProcessEmitToolchain | B_InList          |   361.6 μs |   361.2 μs |  9.2773 |      - |  78.91 KB |
| B_Nextorm_Cached_In_ListContains_Captured | InProcessEmitToolchain | B_InList          |   376.5 μs |   376.8 μs | 11.2305 |      - |  92.11 KB |
| B_Linq2Db_In                              | InProcessEmitToolchain | B_InList          |   723.2 μs |   722.4 μs | 22.4609 |      - | 189.15 KB |
| B_EFCore_In                               | InProcessEmitToolchain | B_InList          | 1,132.1 μs | 1,127.3 μs | 27.3438 | 7.8125 |  227.6 KB |
|                                           |                        |                   |            |            |         |        |           |
| B_Dapper_Intersect                        | InProcessEmitToolchain | B_Intersect       |   167.7 μs |   167.9 μs |  1.4648 |      - |  12.89 KB |
| B_Nextorm_Cached_Intersect                | InProcessEmitToolchain | B_Intersect       |   178.1 μs |   177.9 μs |  7.5684 |      - |  63.59 KB |
| B_Linq2Db_Intersect                       | InProcessEmitToolchain | B_Intersect       |   394.5 μs |   390.2 μs |  5.8594 |      - |  51.25 KB |
| B_EFCore_Intersect                        | InProcessEmitToolchain | B_Intersect       |   765.4 μs |   761.6 μs | 17.5781 | 5.8594 | 143.75 KB |
|                                           |                        |                   |            |            |         |        |           |
| B_Dapper_Join4                            | InProcessEmitToolchain | B_Join4           |   267.0 μs |   266.4 μs |  2.4414 |      - |  20.24 KB |
| B_Nextorm_Cached_Join4                    | InProcessEmitToolchain | B_Join4           |   373.7 μs |   369.6 μs | 12.6953 |      - | 106.73 KB |
| B_Linq2Db_Join4                           | InProcessEmitToolchain | B_Join4           |   721.8 μs |   723.1 μs | 16.6016 |      - | 140.17 KB |
| B_EFCore_Join4                            | InProcessEmitToolchain | B_Join4           | 1,245.1 μs | 1,238.8 μs | 29.2969 | 7.8125 | 248.54 KB |
|                                           |                        |                   |            |            |         |        |           |
| B_Dapper_LeftJoin                         | InProcessEmitToolchain | B_LeftJoin        |   215.0 μs |   215.4 μs |  2.6855 |      - |  22.19 KB |
| B_Nextorm_Cached_LeftJoin                 | InProcessEmitToolchain | B_LeftJoin        |   261.0 μs |   261.7 μs |  7.3242 |      - |  61.64 KB |
| B_Linq2Db_LeftJoin                        | InProcessEmitToolchain | B_LeftJoin        |   757.5 μs |   762.3 μs |  9.7656 |      - |  87.43 KB |
| B_EFCore_LeftJoin                         | InProcessEmitToolchain | B_LeftJoin        | 1,011.5 μs | 1,010.8 μs | 23.4375 | 7.8125 | 201.97 KB |
|                                           |                        |                   |            |            |         |        |           |
| B_Dapper_RecursiveCte                     | InProcessEmitToolchain | B_RecursiveCte    |   266.7 μs |   267.7 μs |  1.4648 |      - |  15.31 KB |
| B_Nextorm_Cached_RecursiveCte             | InProcessEmitToolchain | B_RecursiveCte    |   411.7 μs |   410.9 μs | 16.1133 |      - | 133.13 KB |
| B_Linq2Db_RecursiveCte                    | InProcessEmitToolchain | B_RecursiveCte    |   658.2 μs |   658.7 μs |  9.7656 |      - |  85.32 KB |
|                                           |                        |                   |            |            |         |        |           |
| B_Nextorm_Cached_Contains                 | InProcessEmitToolchain | B_StringFunctions |   192.0 μs |   194.2 μs |  4.8828 |      - |  42.03 KB |
| B_Nextorm_Cached_ToUpper                  | InProcessEmitToolchain | B_StringFunctions |   275.9 μs |   286.1 μs |  4.8828 |      - |  42.81 KB |
| B_Dapper_Contains                         | InProcessEmitToolchain | B_StringFunctions |   376.6 μs |   382.0 μs |  1.9531 |      - |  17.19 KB |
| B_Dapper_ToUpper                          | InProcessEmitToolchain | B_StringFunctions |   422.2 μs |   314.9 μs |  1.4648 |      - |  12.81 KB |
| B_Linq2Db_Contains                        | InProcessEmitToolchain | B_StringFunctions |   613.6 μs |   612.7 μs |  4.8828 |      - |  47.82 KB |
| B_Linq2Db_ToUpper                         | InProcessEmitToolchain | B_StringFunctions |   712.7 μs |   736.5 μs |  7.8125 |      - |  63.91 KB |
| B_EFCore_Contains                         | InProcessEmitToolchain | B_StringFunctions |   854.5 μs |   857.3 μs | 15.6250 | 3.9063 | 140.09 KB |
| B_EFCore_ToUpper                          | InProcessEmitToolchain | B_StringFunctions | 3,736.8 μs | 3,616.3 μs | 15.6250 | 3.9063 | 133.53 KB |
|                                           |                        |                   |            |            |         |        |           |
| B_Dapper_Udf                              | InProcessEmitToolchain | B_Udf             |   170.4 μs |   167.7 μs |  1.4648 |      - |  12.81 KB |
| B_Nextorm_Cached_Udf                      | InProcessEmitToolchain | B_Udf             |   197.9 μs |   200.3 μs |  5.3711 |      - |  44.77 KB |
| B_Linq2Db_Udf                             | InProcessEmitToolchain | B_Udf             |   553.7 μs |   553.2 μs |  7.8125 |      - |  63.91 KB |
| B_EFCore_Udf                              | InProcessEmitToolchain | B_Udf             |   811.3 μs |   820.5 μs | 15.6250 | 4.8828 | 133.52 KB |
|                                           |                        |                   |            |            |         |        |           |
| B_Dapper_SumOver                          | InProcessEmitToolchain | B_Window          |   334.6 μs |   327.8 μs |  1.4648 |      - |  15.47 KB |
| B_Nextorm_Cached_RowNumber                | InProcessEmitToolchain | B_Window          |   428.1 μs |   385.9 μs |  5.8594 |      - |  50.31 KB |
| B_Nextorm_Cached_SumOver                  | InProcessEmitToolchain | B_Window          |   436.2 μs |   439.1 μs |  5.8594 |      - |  52.27 KB |
| B_Dapper_RowNumber                        | InProcessEmitToolchain | B_Window          |   534.6 μs |   567.4 μs |  0.9766 |      - |   15.4 KB |
| B_Linq2Db_RowNumber                       | InProcessEmitToolchain | B_Window          | 1,168.0 μs | 1,251.8 μs |  6.8359 |      - |  60.94 KB |
| B_Linq2Db_SumOver                         | InProcessEmitToolchain | B_Window          | 1,706.7 μs | 1,546.6 μs |  5.8594 |      - |  58.21 KB |
