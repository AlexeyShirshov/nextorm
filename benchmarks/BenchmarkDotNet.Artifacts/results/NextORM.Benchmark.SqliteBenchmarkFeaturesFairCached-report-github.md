```

BenchmarkDotNet v0.15.8, Linux Ubuntu 26.04.1 LTS (Resolute Raccoon)
AMD EPYC 7763 2.62GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method                                    | Categories        | Mean       | Gen0    | Gen1   | Allocated |
|------------------------------------------ |------------------ |-----------:|--------:|-------:|----------:|
| B_Nextorm_Cached_CaseWhen                 | B_CaseWhen        |   126.9 μs |  1.2207 |      - |  20.39 KB |
| B_Dapper_CaseWhen                         | B_CaseWhen        |   148.9 μs |  0.7324 |      - |  13.36 KB |
| B_Linq2Db_CaseWhen                        | B_CaseWhen        |   249.3 μs |  1.9531 |      - |  37.74 KB |
| B_EFCore_CaseWhen                         | B_CaseWhen        |   606.2 μs |  5.8594 | 1.9531 | 114.62 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_Cte                              | B_Cte             |   226.0 μs |  0.7324 |      - |  15.63 KB |
| B_Nextorm_Cached_Cte                      | B_Cte             |   315.4 μs |  5.3711 |      - |  92.74 KB |
| B_Linq2Db_Cte                             | B_Cte             |   601.5 μs |  4.8828 |      - |  90.16 KB |
|                                           |                   |            |         |        |           |
| B_Nextorm_Cached_Distinct                 | B_Distinct        |   155.6 μs |  1.7090 |      - |  29.84 KB |
| B_Dapper_Distinct                         | B_Distinct        |   179.0 μs |  0.7324 |      - |  14.45 KB |
| B_Linq2Db_Distinct                        | B_Distinct        |   306.4 μs |  1.9531 |      - |  38.75 KB |
| B_EFCore_Distinct                         | B_Distinct        |   685.8 μs |  6.8359 | 2.9297 | 116.96 KB |
|                                           |                   |            |         |        |           |
| B_Nextorm_Cached_Except                   | B_Except          |   173.8 μs |  2.4414 |      - |  41.17 KB |
| B_Dapper_Except                           | B_Except          |   185.9 μs |  0.7324 |      - |   14.3 KB |
| B_Linq2Db_Except                          | B_Except          |   406.9 μs |  2.9297 |      - |  50.08 KB |
| B_EFCore_Except                           | B_Except          |   808.3 μs |  8.7891 | 3.9063 | 147.35 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_In                               | B_InList          |   194.6 μs |  0.7324 |      - |  12.58 KB |
| B_Nextorm_Cached_In_AtIn_Inline           | B_InList          |   438.5 μs |  4.8828 |      - |  85.94 KB |
| B_Nextorm_Cached_In_AtIn_Captured         | B_InList          |   444.3 μs |  5.8594 |      - | 103.05 KB |
| B_Nextorm_Cached_In_ListContains_Inline   | B_InList          |   459.8 μs |  4.8828 |      - |  87.58 KB |
| B_Nextorm_Cached_In_ListContains_Captured | B_InList          |   466.2 μs |  6.3477 |      - | 105.32 KB |
| B_Linq2Db_In                              | B_InList          |   746.4 μs |  9.7656 |      - | 172.82 KB |
| B_EFCore_In                               | B_InList          | 1,162.5 μs | 11.7188 | 5.8594 | 220.48 KB |
|                                           |                   |            |         |        |           |
| B_Nextorm_Cached_Intersect                | B_Intersect       |   163.1 μs |  2.4414 |      - |  41.02 KB |
| B_Dapper_Intersect                        | B_Intersect       |   171.4 μs |  0.7324 |      - |  12.81 KB |
| B_Linq2Db_Intersect                       | B_Intersect       |   393.3 μs |  2.9297 |      - |  49.61 KB |
| B_EFCore_Intersect                        | B_Intersect       |   788.3 μs |  7.8125 | 3.9063 | 138.05 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_Join4                            | B_Join4           |   280.6 μs |  0.9766 |      - |  20.16 KB |
| B_Nextorm_Cached_Join4                    | B_Join4           |   352.1 μs |  5.8594 |      - |  99.22 KB |
| B_Linq2Db_Join4                           | B_Join4           |   701.5 μs |  7.8125 |      - | 141.49 KB |
| B_EFCore_Join4                            | B_Join4           | 1,271.0 μs | 13.6719 | 5.8594 |  239.7 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_LeftJoin                         | B_LeftJoin        |   216.0 μs |  1.2207 |      - |  22.11 KB |
| B_Nextorm_Cached_LeftJoin                 | B_LeftJoin        |   238.3 μs |  3.1738 |      - |  54.85 KB |
| B_Linq2Db_LeftJoin                        | B_LeftJoin        |   765.6 μs |  4.8828 |      - |  90.16 KB |
| B_EFCore_LeftJoin                         | B_LeftJoin        | 1,042.7 μs | 11.7188 | 5.8594 | 196.58 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_RecursiveCte                     | B_RecursiveCte    |   263.2 μs |  0.4883 |      - |  15.24 KB |
| B_Nextorm_Cached_RecursiveCte             | B_RecursiveCte    |   339.3 μs |  5.8594 |      - |  98.44 KB |
| B_Linq2Db_RecursiveCte                    | B_RecursiveCte    |   673.9 μs |  4.8828 |      - |  86.18 KB |
|                                           |                   |            |         |        |           |
| B_Nextorm_Cached_Contains                 | B_StringFunctions |   134.5 μs |  1.4648 |      - |  27.81 KB |
| B_Nextorm_Cached_ToUpper                  | B_StringFunctions |   137.1 μs |  1.7090 |      - |  28.36 KB |
| B_Dapper_ToUpper                          | B_StringFunctions |   147.3 μs |  0.7324 |      - |  12.74 KB |
| B_Dapper_Contains                         | B_StringFunctions |   200.7 μs |  0.9766 |      - |  17.11 KB |
| B_Linq2Db_Contains                        | B_StringFunctions |   316.3 μs |  2.4414 |      - |  47.42 KB |
| B_Linq2Db_ToUpper                         | B_StringFunctions |   502.6 μs |  3.9063 |      - |  66.33 KB |
| B_EFCore_ToUpper                          | B_StringFunctions |   702.2 μs |  6.8359 | 2.9297 | 127.43 KB |
| B_EFCore_Contains                         | B_StringFunctions |   724.2 μs |  7.8125 | 3.9063 | 134.77 KB |
|                                           |                   |            |         |        |           |
| B_Nextorm_Cached_Udf                      | B_Udf             |   138.6 μs |  1.7090 |      - |  28.67 KB |
| B_Dapper_Udf                              | B_Udf             |   146.7 μs |  0.7324 |      - |  12.74 KB |
| B_Linq2Db_Udf                             | B_Udf             |   503.1 μs |  3.9063 |      - |  66.33 KB |
| B_EFCore_Udf                              | B_Udf             |   702.2 μs |  6.8359 | 2.9297 | 127.43 KB |
|                                           |                   |            |         |        |           |
| B_Nextorm_Cached_SumOver                  | B_Window          |   298.0 μs |  2.4414 |      - |  41.64 KB |
| B_Nextorm_Cached_RowNumber                | B_Window          |   298.2 μs |  2.4414 |      - |  40.24 KB |
| B_Dapper_SumOver                          | B_Window          |   328.3 μs |  0.4883 |      - |  15.39 KB |
| B_Dapper_RowNumber                        | B_Window          |   335.4 μs |  0.4883 |      - |  15.31 KB |
| B_Linq2Db_SumOver                         | B_Window          |   676.1 μs |  2.9297 |      - |   57.9 KB |
| B_Linq2Db_RowNumber                       | B_Window          |   691.2 μs |  2.9297 |      - |  60.63 KB |
