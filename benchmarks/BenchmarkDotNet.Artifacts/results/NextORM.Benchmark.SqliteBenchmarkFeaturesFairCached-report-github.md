```

BenchmarkDotNet v0.15.8, Linux Ubuntu 26.04.1 LTS (Resolute Raccoon)
AMD EPYC 7763 2.68GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method                                    | Categories        | Mean       | Gen0    | Gen1   | Allocated |
|------------------------------------------ |------------------ |-----------:|--------:|-------:|----------:|
| B_Nextorm_Cached_CaseWhen                 | B_CaseWhen        |   126.4 μs |  1.2207 |      - |  20.63 KB |
| B_Dapper_CaseWhen                         | B_CaseWhen        |   145.6 μs |  0.7324 |      - |  13.36 KB |
| B_Linq2Db_CaseWhen                        | B_CaseWhen        |   247.4 μs |  1.9531 |      - |  37.74 KB |
| B_EFCore_CaseWhen                         | B_CaseWhen        |   588.7 μs |  5.8594 | 1.9531 | 114.62 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_Cte                              | B_Cte             |   229.2 μs |  0.7324 |      - |  15.63 KB |
| B_Nextorm_Cached_Cte                      | B_Cte             |   336.8 μs |  5.3711 |      - |  92.89 KB |
| B_Linq2Db_Cte                             | B_Cte             |   614.8 μs |  4.8828 |      - |  91.72 KB |
|                                           |                   |            |         |        |           |
| B_Nextorm_Cached_Distinct                 | B_Distinct        |   160.7 μs |  1.7090 |      - |  30.31 KB |
| B_Dapper_Distinct                         | B_Distinct        |   180.1 μs |  0.7324 |      - |  14.45 KB |
| B_Linq2Db_Distinct                        | B_Distinct        |   313.8 μs |  1.9531 |      - |  39.85 KB |
| B_EFCore_Distinct                         | B_Distinct        |   661.6 μs |  6.8359 | 2.9297 | 116.96 KB |
|                                           |                   |            |         |        |           |
| B_Nextorm_Cached_Except                   | B_Except          |   173.0 μs |  2.4414 |      - |  41.88 KB |
| B_Dapper_Except                           | B_Except          |   180.7 μs |  0.7324 |      - |   14.3 KB |
| B_Linq2Db_Except                          | B_Except          |   401.8 μs |  2.9297 |      - |  50.08 KB |
| B_EFCore_Except                           | B_Except          |   777.3 μs |  8.7891 | 3.9063 | 147.35 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_In                               | B_InList          |   191.2 μs |  0.7324 |      - |  12.58 KB |
| B_Nextorm_Cached_In_AtIn_Inline           | B_InList          |   355.3 μs |  3.9063 |      - |  70.39 KB |
| B_Nextorm_Cached_In_ListContains_Inline   | B_InList          |   386.3 μs |  4.3945 |      - |   74.3 KB |
| B_Nextorm_Cached_In_AtIn_Captured         | B_InList          |   388.4 μs |  5.3711 |      - |  89.07 KB |
| B_Nextorm_Cached_In_ListContains_Captured | B_InList          |   419.6 μs |  5.3711 |      - |   93.6 KB |
| B_Linq2Db_In                              | B_InList          |   743.6 μs |  9.7656 |      - | 172.82 KB |
| B_EFCore_In                               | B_InList          | 1,152.7 μs | 11.7188 | 5.8594 | 223.06 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_Intersect                        | B_Intersect       |   169.1 μs |  0.7324 |      - |  12.81 KB |
| B_Nextorm_Cached_Intersect                | B_Intersect       |   173.1 μs |  2.4414 |      - |  41.72 KB |
| B_Linq2Db_Intersect                       | B_Intersect       |   395.9 μs |  2.9297 |      - |  49.61 KB |
| B_EFCore_Intersect                        | B_Intersect       |   764.3 μs |  7.8125 | 3.9063 | 138.05 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_Join4                            | B_Join4           |   280.5 μs |  0.9766 |      - |  20.16 KB |
| B_Nextorm_Cached_Join4                    | B_Join4           |   363.6 μs |  5.8594 |      - |  98.52 KB |
| B_Linq2Db_Join4                           | B_Join4           |   702.2 μs |  7.8125 |      - | 139.77 KB |
| B_EFCore_Join4                            | B_Join4           | 1,241.2 μs | 13.6719 | 5.8594 | 238.99 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_LeftJoin                         | B_LeftJoin        |   209.8 μs |  1.2207 |      - |  22.11 KB |
| B_Nextorm_Cached_LeftJoin                 | B_LeftJoin        |   257.8 μs |  3.4180 |      - |  57.11 KB |
| B_Linq2Db_LeftJoin                        | B_LeftJoin        |   748.3 μs |  4.8828 |      - |  89.38 KB |
| B_EFCore_LeftJoin                         | B_LeftJoin        | 1,023.5 μs | 11.7188 | 5.8594 | 196.58 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_RecursiveCte                     | B_RecursiveCte    |   262.8 μs |  0.4883 |      - |  15.24 KB |
| B_Nextorm_Cached_RecursiveCte             | B_RecursiveCte    |   354.6 μs |  5.8594 |      - |  99.85 KB |
| B_Linq2Db_RecursiveCte                    | B_RecursiveCte    |   645.3 μs |  4.8828 |      - |  83.13 KB |
|                                           |                   |            |         |        |           |
| B_Nextorm_Cached_Contains                 | B_StringFunctions |   141.6 μs |  1.7090 |      - |  28.13 KB |
| B_Nextorm_Cached_ToUpper                  | B_StringFunctions |   142.8 μs |  1.7090 |      - |  28.67 KB |
| B_Dapper_ToUpper                          | B_StringFunctions |   143.8 μs |  0.7324 |      - |  12.74 KB |
| B_Dapper_Contains                         | B_StringFunctions |   199.3 μs |  0.9766 |      - |  17.11 KB |
| B_Linq2Db_Contains                        | B_StringFunctions |   322.5 μs |  2.4414 |      - |  47.42 KB |
| B_Linq2Db_ToUpper                         | B_StringFunctions |   508.9 μs |  3.9063 |      - |  66.33 KB |
| B_EFCore_ToUpper                          | B_StringFunctions |   684.2 μs |  6.8359 | 2.9297 | 127.43 KB |
| B_EFCore_Contains                         | B_StringFunctions |   727.8 μs |  7.8125 | 3.9063 | 134.77 KB |
|                                           |                   |            |         |        |           |
| B_Dapper_Udf                              | B_Udf             |   144.1 μs |  0.7324 |      - |  12.74 KB |
| B_Nextorm_Cached_Udf                      | B_Udf             |   145.9 μs |  1.7090 |      - |  28.99 KB |
| B_Linq2Db_Udf                             | B_Udf             |   519.4 μs |  3.9063 |      - |  66.33 KB |
| B_EFCore_Udf                              | B_Udf             |   688.4 μs |  6.8359 | 2.9297 | 127.43 KB |
|                                           |                   |            |         |        |           |
| B_Nextorm_Cached_RowNumber                | B_Window          |   309.6 μs |  2.4414 |      - |  40.86 KB |
| B_Nextorm_Cached_SumOver                  | B_Window          |   310.2 μs |  2.4414 |      - |  41.88 KB |
| B_Dapper_SumOver                          | B_Window          |   325.6 μs |  0.4883 |      - |  15.39 KB |
| B_Dapper_RowNumber                        | B_Window          |   326.8 μs |  0.4883 |      - |  15.31 KB |
| B_Linq2Db_SumOver                         | B_Window          |   676.9 μs |  2.9297 |      - |   57.9 KB |
| B_Linq2Db_RowNumber                       | B_Window          |   682.5 μs |  2.9297 |      - |  59.54 KB |
