```

BenchmarkDotNet v0.15.8, Linux Ubuntu 26.04.1 LTS (Resolute Raccoon)
INTEL XEON PLATINUM 8573C 3.58GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v4

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method                                    | Categories        | Mean      | Gen0   | Gen1   | Allocated |
|------------------------------------------ |------------------ |----------:|-------:|-------:|----------:|
| B_Nextorm_Cached_CaseWhen                 | B_CaseWhen        |  57.98 μs | 0.2441 |      - |  20.63 KB |
| B_Dapper_CaseWhen                         | B_CaseWhen        |  69.11 μs | 0.1221 |      - |  13.36 KB |
| B_Linq2Db_CaseWhen                        | B_CaseWhen        |  95.80 μs | 0.3662 |      - |  37.74 KB |
| B_EFCore_CaseWhen                         | B_CaseWhen        | 259.16 μs | 0.9766 |      - | 114.61 KB |
|                                           |                   |           |        |        |           |
| B_Dapper_Cte                              | B_Cte             | 112.13 μs | 0.1221 |      - |  15.63 KB |
| B_Nextorm_Cached_Cte                      | B_Cte             | 129.29 μs | 0.9766 |      - |  92.03 KB |
| B_Linq2Db_Cte                             | B_Cte             | 212.60 μs | 0.9766 |      - |  90.16 KB |
|                                           |                   |           |        |        |           |
| B_Nextorm_Cached_Distinct                 | B_Distinct        |  72.28 μs | 0.3662 |      - |  30.31 KB |
| B_Dapper_Distinct                         | B_Distinct        |  80.41 μs | 0.1221 |      - |  14.45 KB |
| B_Linq2Db_Distinct                        | B_Distinct        | 111.31 μs | 0.3662 |      - |  38.75 KB |
| B_EFCore_Distinct                         | B_Distinct        | 273.05 μs | 0.9766 | 0.4883 | 116.96 KB |
|                                           |                   |           |        |        |           |
| B_Nextorm_Cached_Except                   | B_Except          |  87.23 μs | 0.4883 |      - |  41.88 KB |
| B_Dapper_Except                           | B_Except          |  97.23 μs | 0.1221 |      - |   14.3 KB |
| B_Linq2Db_Except                          | B_Except          | 159.40 μs | 0.4883 |      - |  50.08 KB |
| B_EFCore_Except                           | B_Except          | 350.04 μs | 1.4648 | 0.9766 | 147.35 KB |
|                                           |                   |           |        |        |           |
| B_Dapper_In                               | B_InList          |  86.88 μs | 0.1221 |      - |  12.58 KB |
| B_Nextorm_Cached_In_AtIn_Inline           | B_InList          | 135.49 μs | 0.7324 |      - |  71.56 KB |
| B_Nextorm_Cached_In_AtIn_Captured         | B_InList          | 139.98 μs | 0.9766 |      - |  90.24 KB |
| B_Nextorm_Cached_In_ListContains_Inline   | B_InList          | 142.18 μs | 0.7324 |      - |  72.89 KB |
| B_Nextorm_Cached_In_ListContains_Captured | B_InList          | 145.14 μs | 0.9766 |      - |  92.19 KB |
| B_Linq2Db_In                              | B_InList          | 212.43 μs | 1.9531 |      - | 172.81 KB |
| B_EFCore_In                               | B_InList          | 429.93 μs | 2.4414 | 0.9766 | 221.57 KB |
|                                           |                   |           |        |        |           |
| B_Nextorm_Cached_Intersect                | B_Intersect       |  76.23 μs | 0.4883 |      - |  41.72 KB |
| B_Dapper_Intersect                        | B_Intersect       |  84.87 μs | 0.1221 |      - |  12.81 KB |
| B_Linq2Db_Intersect                       | B_Intersect       | 146.56 μs | 0.4883 |      - |  49.61 KB |
| B_EFCore_Intersect                        | B_Intersect       | 330.69 μs | 1.4648 | 0.4883 | 138.05 KB |
|                                           |                   |           |        |        |           |
| B_Dapper_Join4                            | B_Join4           | 170.62 μs | 0.2441 |      - |  20.16 KB |
| B_Nextorm_Cached_Join4                    | B_Join4           | 176.52 μs | 0.9766 |      - |  99.53 KB |
| B_Linq2Db_Join4                           | B_Join4           | 302.96 μs | 1.4648 |      - | 138.99 KB |
| B_EFCore_Join4                            | B_Join4           | 597.00 μs | 2.9297 | 0.9766 | 241.17 KB |
|                                           |                   |           |        |        |           |
| B_Dapper_LeftJoin                         | B_LeftJoin        | 121.01 μs | 0.2441 |      - |  22.11 KB |
| B_Nextorm_Cached_LeftJoin                 | B_LeftJoin        | 123.55 μs | 0.4883 |      - |   55.7 KB |
| B_Linq2Db_LeftJoin                        | B_LeftJoin        | 266.49 μs | 0.9766 |      - |  89.38 KB |
| B_EFCore_LeftJoin                         | B_LeftJoin        | 450.23 μs | 2.4414 | 0.9766 |    200 KB |
|                                           |                   |           |        |        |           |
| B_Dapper_RecursiveCte                     | B_RecursiveCte    | 126.12 μs |      - |      - |  15.24 KB |
| B_Nextorm_Cached_RecursiveCte             | B_RecursiveCte    | 143.70 μs | 1.2207 |      - | 100.16 KB |
| B_Linq2Db_RecursiveCte                    | B_RecursiveCte    | 233.24 μs | 0.9766 |      - |  83.13 KB |
|                                           |                   |           |        |        |           |
| B_Dapper_ToUpper                          | B_StringFunctions |  64.02 μs | 0.1221 |      - |  12.74 KB |
| B_Nextorm_Cached_ToUpper                  | B_StringFunctions |  64.33 μs | 0.2441 |      - |  28.75 KB |
| B_Nextorm_Cached_Contains                 | B_StringFunctions |  64.64 μs | 0.2441 |      - |   29.3 KB |
| B_Dapper_Contains                         | B_StringFunctions |  92.73 μs | 0.1221 |      - |  17.11 KB |
| B_Linq2Db_Contains                        | B_StringFunctions | 116.23 μs | 0.4883 |      - |  48.83 KB |
| B_Linq2Db_ToUpper                         | B_StringFunctions | 159.70 μs | 0.7324 |      - |  66.33 KB |
| B_EFCore_ToUpper                          | B_StringFunctions | 285.59 μs | 1.4648 | 0.4883 | 127.42 KB |
| B_EFCore_Contains                         | B_StringFunctions | 295.29 μs | 1.4648 | 0.4883 | 135.86 KB |
|                                           |                   |           |        |        |           |
| B_Dapper_Udf                              | B_Udf             |  64.68 μs | 0.1221 |      - |  12.74 KB |
| B_Nextorm_Cached_Udf                      | B_Udf             |  64.78 μs | 0.2441 |      - |  29.06 KB |
| B_Linq2Db_Udf                             | B_Udf             | 160.62 μs | 0.7324 |      - |  66.33 KB |
| B_EFCore_Udf                              | B_Udf             | 284.49 μs | 1.4648 | 0.4883 | 127.42 KB |
|                                           |                   |           |        |        |           |
| B_Nextorm_Cached_SumOver                  | B_Window          | 136.08 μs | 0.4883 |      - |  41.88 KB |
| B_Nextorm_Cached_RowNumber                | B_Window          | 138.34 μs | 0.4883 |      - |  41.64 KB |
| B_Dapper_SumOver                          | B_Window          | 144.37 μs |      - |      - |  15.39 KB |
| B_Dapper_RowNumber                        | B_Window          | 145.59 μs |      - |      - |  15.31 KB |
| B_Linq2Db_SumOver                         | B_Window          | 215.25 μs | 0.4883 |      - |  57.89 KB |
| B_Linq2Db_RowNumber                       | B_Window          | 215.66 μs | 0.4883 |      - |  59.53 KB |
