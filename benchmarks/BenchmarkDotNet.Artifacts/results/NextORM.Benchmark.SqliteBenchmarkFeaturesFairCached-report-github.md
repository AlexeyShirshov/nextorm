```

BenchmarkDotNet v0.15.8, Linux Ubuntu 24.04.5 LTS (Noble Numbat)
AMD EPYC 9V74 3.69GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.403
  [Host] : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method                                    | Categories        | Mean     | Gen0    | Gen1   | Allocated |
|------------------------------------------ |------------------ |---------:|--------:|-------:|----------:|
| B_Nextorm_Cached_CaseWhen                 | B_CaseWhen        | 113.3 μs |  2.0752 |      - |  35.16 KB |
| B_Dapper_CaseWhen                         | B_CaseWhen        | 121.9 μs |  0.7324 |      - |  13.44 KB |
| B_Linq2Db_CaseWhen                        | B_CaseWhen        | 160.3 μs |  2.1973 |      - |  38.05 KB |
| B_EFCore_CaseWhen                         | B_CaseWhen        | 336.9 μs |  6.8359 | 3.4180 | 119.53 KB |
|                                           |                   |          |         |        |           |
| B_Dapper_Cte                              | B_Cte             | 165.4 μs |  0.7324 |      - |   15.7 KB |
| B_Nextorm_Cached_Cte                      | B_Cte             | 216.9 μs |  7.0801 |      - | 118.29 KB |
| B_Linq2Db_Cte                             | B_Cte             | 331.0 μs |  5.3711 |      - |  92.03 KB |
|                                           |                   |          |         |        |           |
| B_Nextorm_Cached_Distinct                 | B_Distinct        | 126.7 μs |  2.4414 |      - |  41.49 KB |
| B_Dapper_Distinct                         | B_Distinct        | 141.9 μs |  0.7324 |      - |  15.55 KB |
| B_Linq2Db_Distinct                        | B_Distinct        | 177.0 μs |  2.1973 |      - |  39.14 KB |
| B_EFCore_Distinct                         | B_Distinct        | 375.3 μs |  7.3242 | 3.4180 | 121.96 KB |
|                                           |                   |          |         |        |           |
| B_Dapper_Except                           | B_Except          | 144.6 μs |  0.7324 |      - |  14.38 KB |
| B_Nextorm_Cached_Except                   | B_Except          | 144.6 μs |  4.1504 |      - |  69.45 KB |
| B_Linq2Db_Except                          | B_Except          | 219.3 μs |  2.9297 |      - |  50.55 KB |
| B_EFCore_Except                           | B_Except          | 456.5 μs |  9.2773 | 4.3945 | 153.05 KB |
|                                           |                   |          |         |        |           |
| B_Dapper_In                               | B_InList          | 141.3 μs |  0.7324 |      - |  12.66 KB |
| B_Nextorm_Cached_In_AtIn_Inline           | B_InList          | 203.4 μs |  4.8828 |      - |  79.92 KB |
| B_Nextorm_Cached_In_ListContains_Inline   | B_InList          | 206.3 μs |  4.8828 |      - |  81.25 KB |
| B_Nextorm_Cached_In_AtIn_Captured         | B_InList          | 210.0 μs |  5.8594 |      - |  98.44 KB |
| B_Nextorm_Cached_In_ListContains_Captured | B_InList          | 214.7 μs |  6.1035 |      - | 100.39 KB |
| B_Linq2Db_In                              | B_InList          | 426.2 μs | 11.2305 |      - | 189.14 KB |
| B_EFCore_In                               | B_InList          | 803.7 μs | 13.6719 | 6.8359 |  226.1 KB |
|                                           |                   |          |         |        |           |
| B_Dapper_Intersect                        | B_Intersect       | 135.5 μs |  0.7324 |      - |  12.89 KB |
| B_Nextorm_Cached_Intersect                | B_Intersect       | 138.5 μs |  4.1504 |      - |   69.3 KB |
| B_Linq2Db_Intersect                       | B_Intersect       | 213.7 μs |  2.9297 |      - |  50.08 KB |
| B_EFCore_Intersect                        | B_Intersect       | 443.1 μs |  8.7891 | 4.3945 | 143.75 KB |
|                                           |                   |          |         |        |           |
| B_Dapper_Join4                            | B_Join4           | 210.2 μs |  1.2207 |      - |  20.24 KB |
| B_Nextorm_Cached_Join4                    | B_Join4           | 243.8 μs |  7.0801 |      - | 115.79 KB |
| B_Linq2Db_Join4                           | B_Join4           | 394.9 μs |  8.3008 |      - | 139.46 KB |
| B_EFCore_Join4                            | B_Join4           | 815.3 μs | 14.6484 | 4.8828 | 245.87 KB |
|                                           |                   |          |         |        |           |
| B_Dapper_LeftJoin                         | B_LeftJoin        | 166.8 μs |  1.2207 |      - |  22.19 KB |
| B_Nextorm_Cached_LeftJoin                 | B_LeftJoin        | 174.3 μs |  3.9063 |      - |  65.78 KB |
| B_Linq2Db_LeftJoin                        | B_LeftJoin        | 397.0 μs |  4.8828 |      - |  86.02 KB |
| B_EFCore_LeftJoin                         | B_LeftJoin        | 639.6 μs | 11.7188 | 5.8594 | 205.48 KB |
|                                           |                   |          |         |        |           |
| B_Dapper_RecursiveCte                     | B_RecursiveCte    | 179.8 μs |  0.7324 |      - |  15.31 KB |
| B_Nextorm_Cached_RecursiveCte             | B_RecursiveCte    | 249.1 μs |  8.5449 |      - | 142.81 KB |
| B_Linq2Db_RecursiveCte                    | B_RecursiveCte    | 348.0 μs |  4.8828 |      - |  85.39 KB |
|                                           |                   |          |         |        |           |
| B_Dapper_ToUpper                          | B_StringFunctions | 118.4 μs |  0.7324 |      - |  12.81 KB |
| B_Nextorm_Cached_Contains                 | B_StringFunctions | 121.8 μs |  2.6855 |      - |  45.16 KB |
| B_Nextorm_Cached_ToUpper                  | B_StringFunctions | 123.2 μs |  2.6855 |      - |  45.16 KB |
| B_Dapper_Contains                         | B_StringFunctions | 149.0 μs |  0.9766 |      - |  17.19 KB |
| B_Linq2Db_Contains                        | B_StringFunctions | 181.2 μs |  2.6855 |      - |  47.81 KB |
| B_Linq2Db_ToUpper                         | B_StringFunctions | 245.6 μs |  3.9063 |      - |  63.91 KB |
| B_EFCore_ToUpper                          | B_StringFunctions | 396.5 μs |  7.8125 | 3.9063 | 133.91 KB |
| B_EFCore_Contains                         | B_StringFunctions | 407.8 μs |  8.3008 | 3.9063 | 140.08 KB |
|                                           |                   |          |         |        |           |
| B_Dapper_Udf                              | B_Udf             | 118.7 μs |  0.7324 |      - |  12.81 KB |
| B_Nextorm_Cached_Udf                      | B_Udf             | 123.8 μs |  2.6855 |      - |  46.02 KB |
| B_Linq2Db_Udf                             | B_Udf             | 244.7 μs |  3.9063 |      - |  63.91 KB |
| B_EFCore_Udf                              | B_Udf             | 389.0 μs |  7.8125 | 3.9063 | 133.91 KB |
|                                           |                   |          |         |        |           |
| B_Dapper_SumOver                          | B_Window          | 204.3 μs |  0.7324 |      - |  15.47 KB |
| B_Nextorm_Cached_RowNumber                | B_Window          | 205.6 μs |  3.1738 |      - |  54.22 KB |
| B_Dapper_RowNumber                        | B_Window          | 205.7 μs |  0.7324 |      - |  15.39 KB |
| B_Nextorm_Cached_SumOver                  | B_Window          | 206.3 μs |  3.4180 |      - |  56.17 KB |
| B_Linq2Db_RowNumber                       | B_Window          | 374.4 μs |  3.4180 |      - |  59.85 KB |
| B_Linq2Db_SumOver                         | B_Window          | 381.2 μs |  3.4180 |      - |  58.21 KB |
