```

BenchmarkDotNet v0.15.8, Linux Ubuntu 26.04.1 LTS (Resolute Raccoon)
AMD EPYC 7763 3.24GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method                                    | Categories        | Mean       | Gen0    | Gen1   | Allocated |
|------------------------------------------ |------------------ |-----------:|--------:|-------:|----------:|
| B_Nextorm_Cached_CaseWhen                 | B_CaseWhen        |   123.8 μs |  1.2207 |      - |  20.63 KB |
| B_Dapper_CaseWhen                         | B_CaseWhen        |   145.0 μs |  0.7324 |      - |  13.36 KB |
| B_Linq2Db_CaseWhen                        | B_CaseWhen        |   254.2 μs |  1.9531 |      - |  37.74 KB |
| B_EFCore_CaseWhen                         | B_CaseWhen        |   614.8 μs |  5.8594 | 1.9531 | 114.62 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_Cte                              | B_Cte             |   224.0 μs |  0.7324 |      - |  15.63 KB |
| B_Nextorm_Cached_Cte                      | B_Cte             |   338.7 μs |  5.3711 |      - |   94.3 KB |
| B_Linq2Db_Cte                             | B_Cte             |   609.3 μs |  4.8828 |      - |  91.33 KB |
|                                           |                   |            |         |        |           |
| B_Nextorm_Cached_Distinct                 | B_Distinct        |   162.9 μs |  1.7090 |      - |  30.31 KB |
| B_Dapper_Distinct                         | B_Distinct        |   183.4 μs |  0.7324 |      - |  14.45 KB |
| B_Linq2Db_Distinct                        | B_Distinct        |   314.3 μs |  1.9531 |      - |  38.75 KB |
| B_EFCore_Distinct                         | B_Distinct        |   686.5 μs |  6.8359 | 2.9297 | 116.96 KB |
|                                           |                   |            |         |        |           |
| B_Nextorm_Cached_Except                   | B_Except          |   179.9 μs |  2.4414 |      - |  41.88 KB |
| B_Dapper_Except                           | B_Except          |   183.8 μs |  0.7324 |      - |   14.3 KB |
| B_Linq2Db_Except                          | B_Except          |   424.5 μs |  2.9297 |      - |  50.08 KB |
| B_EFCore_Except                           | B_Except          |   813.3 μs |  8.7891 | 3.9063 | 147.35 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_In                               | B_InList          |   193.7 μs |  0.7324 |      - |  12.58 KB |
| B_Nextorm_Cached_In_AtIn_Inline           | B_InList          |   373.6 μs |  3.9063 |      - |  70.39 KB |
| B_Nextorm_Cached_In_ListContains_Inline   | B_InList          |   386.9 μs |  3.9063 |      - |  71.72 KB |
| B_Nextorm_Cached_In_AtIn_Captured         | B_InList          |   399.4 μs |  5.3711 |      - |  89.07 KB |
| B_Nextorm_Cached_In_ListContains_Captured | B_InList          |   413.7 μs |  5.3711 |      - |  91.02 KB |
| B_Linq2Db_In                              | B_InList          |   754.3 μs |  9.7656 |      - | 173.99 KB |
| B_EFCore_In                               | B_InList          | 1,197.4 μs | 11.7188 | 5.8594 | 222.05 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_Intersect                        | B_Intersect       |   169.7 μs |  0.7324 |      - |  12.81 KB |
| B_Nextorm_Cached_Intersect                | B_Intersect       |   172.2 μs |  2.4414 |      - |  41.72 KB |
| B_Linq2Db_Intersect                       | B_Intersect       |   395.3 μs |  2.9297 |      - |  49.61 KB |
| B_EFCore_Intersect                        | B_Intersect       |   780.8 μs |  7.8125 | 3.9063 | 138.05 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_Join4                            | B_Join4           |   279.7 μs |  0.9766 |      - |  20.16 KB |
| B_Nextorm_Cached_Join4                    | B_Join4           |   377.1 μs |  5.8594 |      - | 100.87 KB |
| B_Linq2Db_Join4                           | B_Join4           |   709.6 μs |  7.8125 |      - | 140.55 KB |
| B_EFCore_Join4                            | B_Join4           | 1,251.3 μs | 13.6719 | 5.8594 | 240.86 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_LeftJoin                         | B_LeftJoin        |   213.8 μs |  1.2207 |      - |  22.11 KB |
| B_Nextorm_Cached_LeftJoin                 | B_LeftJoin        |   264.5 μs |  3.4180 |      - |  56.72 KB |
| B_Linq2Db_LeftJoin                        | B_LeftJoin        |   749.2 μs |  4.8828 |      - |  89.38 KB |
| B_EFCore_LeftJoin                         | B_LeftJoin        | 1,050.1 μs | 11.7188 | 5.8594 | 199.95 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_RecursiveCte                     | B_RecursiveCte    |   270.2 μs |  0.4883 |      - |  15.24 KB |
| B_Nextorm_Cached_RecursiveCte             | B_RecursiveCte    |   367.6 μs |  5.8594 |      - | 101.41 KB |
| B_Linq2Db_RecursiveCte                    | B_RecursiveCte    |   689.9 μs |  4.8828 |      - |  83.13 KB |
|                                           |                   |            |         |        |           |
| B_Nextorm_Cached_Contains                 | B_StringFunctions |   138.3 μs |  1.7090 |      - |  28.13 KB |
| B_Nextorm_Cached_ToUpper                  | B_StringFunctions |   145.2 μs |  1.7090 |      - |  28.67 KB |
| B_Dapper_ToUpper                          | B_StringFunctions |   146.2 μs |  0.7324 |      - |  12.74 KB |
| B_Dapper_Contains                         | B_StringFunctions |   196.7 μs |  0.9766 |      - |  17.11 KB |
| B_Linq2Db_Contains                        | B_StringFunctions |   333.8 μs |  2.9297 |      - |   48.6 KB |
| B_Linq2Db_ToUpper                         | B_StringFunctions |   537.5 μs |  3.9063 |      - |   67.5 KB |
| B_EFCore_ToUpper                          | B_StringFunctions |   724.9 μs |  6.8359 | 2.9297 | 127.43 KB |
| B_EFCore_Contains                         | B_StringFunctions |   744.9 μs |  7.8125 | 3.9063 | 134.77 KB |
|                                           |                   |            |         |        |           |
| B_Nextorm_Cached_Udf                      | B_Udf             |   145.0 μs |  1.7090 |      - |  28.99 KB |
| B_Dapper_Udf                              | B_Udf             |   146.2 μs |  0.7324 |      - |  12.74 KB |
| B_Linq2Db_Udf                             | B_Udf             |   517.8 μs |  3.9063 |      - |   67.5 KB |
| B_EFCore_Udf                              | B_Udf             |   705.1 μs |  6.8359 | 2.9297 | 127.43 KB |
|                                           |                   |            |         |        |           |
| B_Nextorm_Cached_SumOver                  | B_Window          |   315.4 μs |  2.4414 |      - |  41.88 KB |
| B_Nextorm_Cached_RowNumber                | B_Window          |   322.8 μs |  2.4414 |      - |  40.86 KB |
| B_Dapper_SumOver                          | B_Window          |   325.7 μs |  0.4883 |      - |  15.39 KB |
| B_Dapper_RowNumber                        | B_Window          |   335.5 μs |  0.4883 |      - |  15.31 KB |
| B_Linq2Db_SumOver                         | B_Window          |   689.1 μs |  2.9297 |      - |   57.9 KB |
| B_Linq2Db_RowNumber                       | B_Window          |   697.3 μs |  2.9297 |      - |  59.54 KB |
