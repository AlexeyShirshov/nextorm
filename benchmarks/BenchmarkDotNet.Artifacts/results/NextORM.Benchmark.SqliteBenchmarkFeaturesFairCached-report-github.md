```

BenchmarkDotNet v0.15.8, Linux Ubuntu 26.04.1 LTS (Resolute Raccoon)
AMD EPYC 9V74 3.69GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.403
  [Host] : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method                                    | Categories        | Mean     | Gen0    | Gen1   | Allocated |
|------------------------------------------ |------------------ |---------:|--------:|-------:|----------:|
| B_Nextorm_Cached_CaseWhen                 | B_CaseWhen        | 114.8 μs |  2.0752 |      - |  34.69 KB |
| B_Dapper_CaseWhen                         | B_CaseWhen        | 123.6 μs |  0.7324 |      - |  13.44 KB |
| B_Linq2Db_CaseWhen                        | B_CaseWhen        | 158.3 μs |  2.1973 |      - |  38.05 KB |
| B_EFCore_CaseWhen                         | B_CaseWhen        | 341.6 μs |  6.8359 | 3.4180 | 119.53 KB |
|                                           |                   |          |         |        |           |
| B_Dapper_Cte                              | B_Cte             | 170.7 μs |  0.7324 |      - |   15.7 KB |
| B_Nextorm_Cached_Cte                      | B_Cte             | 214.2 μs |  7.0801 |      - | 116.49 KB |
| B_Linq2Db_Cte                             | B_Cte             | 337.0 μs |  5.3711 |      - |  90.63 KB |
|                                           |                   |          |         |        |           |
| B_Nextorm_Cached_Distinct                 | B_Distinct        | 126.7 μs |  2.4414 |      - |  41.56 KB |
| B_Dapper_Distinct                         | B_Distinct        | 139.5 μs |  0.7324 |      - |  15.55 KB |
| B_Linq2Db_Distinct                        | B_Distinct        | 180.7 μs |  2.1973 |      - |  39.14 KB |
| B_EFCore_Distinct                         | B_Distinct        | 373.9 μs |  7.3242 | 3.4180 | 121.96 KB |
|                                           |                   |          |         |        |           |
| B_Nextorm_Cached_Except                   | B_Except          | 141.5 μs |  3.9063 |      - |  63.99 KB |
| B_Dapper_Except                           | B_Except          | 148.1 μs |  0.7324 |      - |  14.38 KB |
| B_Linq2Db_Except                          | B_Except          | 227.6 μs |  2.9297 |      - |  50.55 KB |
| B_EFCore_Except                           | B_Except          | 453.8 μs |  9.2773 | 4.3945 | 153.05 KB |
|                                           |                   |          |         |        |           |
| B_Dapper_In                               | B_InList          | 142.2 μs |  0.7324 |      - |  12.66 KB |
| B_Nextorm_Cached_In_AtIn_Inline           | B_InList          | 198.7 μs |  4.6387 |      - |  79.61 KB |
| B_Nextorm_Cached_In_AtIn_Captured         | B_InList          | 203.7 μs |  5.8594 |      - |  98.13 KB |
| B_Nextorm_Cached_In_ListContains_Inline   | B_InList          | 209.9 μs |  4.8828 |      - |  80.94 KB |
| B_Nextorm_Cached_In_ListContains_Captured | B_InList          | 211.5 μs |  6.1035 |      - | 100.08 KB |
| B_Linq2Db_In                              | B_InList          | 425.2 μs | 11.2305 |      - | 189.14 KB |
| B_EFCore_In                               | B_InList          | 788.9 μs | 13.6719 | 6.8359 | 226.11 KB |
|                                           |                   |          |         |        |           |
| B_Nextorm_Cached_Intersect                | B_Intersect       | 135.8 μs |  3.9063 |      - |  63.83 KB |
| B_Dapper_Intersect                        | B_Intersect       | 137.0 μs |  0.7324 |      - |  12.89 KB |
| B_Linq2Db_Intersect                       | B_Intersect       | 217.2 μs |  2.9297 |      - |  50.08 KB |
| B_EFCore_Intersect                        | B_Intersect       | 439.7 μs |  8.7891 | 4.3945 | 143.75 KB |
|                                           |                   |          |         |        |           |
| B_Dapper_Join4                            | B_Join4           | 215.2 μs |  1.2207 |      - |  20.24 KB |
| B_Nextorm_Cached_Join4                    | B_Join4           | 243.2 μs |  6.8359 |      - | 113.29 KB |
| B_Linq2Db_Join4                           | B_Join4           | 406.4 μs |  8.3008 |      - | 140.24 KB |
| B_EFCore_Join4                            | B_Join4           | 832.7 μs | 14.6484 | 4.8828 | 246.34 KB |
|                                           |                   |          |         |        |           |
| B_Dapper_LeftJoin                         | B_LeftJoin        | 169.2 μs |  1.2207 |      - |  22.19 KB |
| B_Nextorm_Cached_LeftJoin                 | B_LeftJoin        | 180.7 μs |  3.9063 |      - |  65.39 KB |
| B_Linq2Db_LeftJoin                        | B_LeftJoin        | 401.2 μs |  4.8828 |      - |  86.02 KB |
| B_EFCore_LeftJoin                         | B_LeftJoin        | 640.0 μs | 11.7188 | 5.8594 | 201.96 KB |
|                                           |                   |          |         |        |           |
| B_Dapper_RecursiveCte                     | B_RecursiveCte    | 183.1 μs |  0.7324 |      - |  15.31 KB |
| B_Nextorm_Cached_RecursiveCte             | B_RecursiveCte    | 227.3 μs |  8.3008 |      - | 137.11 KB |
| B_Linq2Db_RecursiveCte                    | B_RecursiveCte    | 362.3 μs |  4.8828 |      - |  85.39 KB |
|                                           |                   |          |         |        |           |
| B_Dapper_ToUpper                          | B_StringFunctions | 119.2 μs |  0.7324 |      - |  12.81 KB |
| B_Nextorm_Cached_Contains                 | B_StringFunctions | 123.4 μs |  2.6855 |      - |  44.77 KB |
| B_Nextorm_Cached_ToUpper                  | B_StringFunctions | 124.5 μs |  2.6855 |      - |  45.86 KB |
| B_Dapper_Contains                         | B_StringFunctions | 150.4 μs |  0.9766 |      - |  17.19 KB |
| B_Linq2Db_Contains                        | B_StringFunctions | 183.6 μs |  2.6855 |      - |  47.81 KB |
| B_Linq2Db_ToUpper                         | B_StringFunctions | 249.4 μs |  3.9063 |      - |  63.91 KB |
| B_EFCore_Contains                         | B_StringFunctions | 405.6 μs |  8.3008 | 3.9063 | 140.08 KB |
| B_EFCore_ToUpper                          | B_StringFunctions | 405.6 μs |  7.8125 | 3.9063 | 133.83 KB |
|                                           |                   |          |         |        |           |
| B_Dapper_Udf                              | B_Udf             | 119.0 μs |  0.7324 |      - |  12.81 KB |
| B_Nextorm_Cached_Udf                      | B_Udf             | 124.6 μs |  2.6855 |      - |  45.63 KB |
| B_Linq2Db_Udf                             | B_Udf             | 250.4 μs |  3.9063 |      - |  63.91 KB |
| B_EFCore_Udf                              | B_Udf             | 397.0 μs |  7.8125 | 3.9063 | 133.83 KB |
|                                           |                   |          |         |        |           |
| B_Nextorm_Cached_RowNumber                | B_Window          | 205.1 μs |  3.1738 |      - |  53.75 KB |
| B_Dapper_SumOver                          | B_Window          | 205.4 μs |  0.7324 |      - |  15.47 KB |
| B_Dapper_RowNumber                        | B_Window          | 206.6 μs |  0.7324 |      - |  15.39 KB |
| B_Nextorm_Cached_SumOver                  | B_Window          | 206.6 μs |  3.1738 |      - |   55.7 KB |
| B_Linq2Db_SumOver                         | B_Window          | 369.2 μs |  3.4180 |      - |  58.21 KB |
| B_Linq2Db_RowNumber                       | B_Window          | 369.4 μs |  3.4180 |      - |  59.85 KB |
