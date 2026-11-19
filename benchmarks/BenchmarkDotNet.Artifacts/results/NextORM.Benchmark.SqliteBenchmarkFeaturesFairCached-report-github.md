```

BenchmarkDotNet v0.15.8, Linux Ubuntu 26.04.1 LTS (Resolute Raccoon)
AMD EPYC 9V45 4.25GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v4

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method                                    | Categories        | Mean      | Gen0    | Gen1   | Allocated |
|------------------------------------------ |------------------ |----------:|--------:|-------:|----------:|
| B_Nextorm_Cached_CaseWhen                 | B_CaseWhen        |  88.25 μs |  1.2207 |      - |  20.63 KB |
| B_Dapper_CaseWhen                         | B_CaseWhen        |  96.50 μs |  0.7324 |      - |  13.36 KB |
| B_Linq2Db_CaseWhen                        | B_CaseWhen        | 115.91 μs |  2.1973 |      - |  37.73 KB |
| B_EFCore_CaseWhen                         | B_CaseWhen        | 219.77 μs |  6.8359 | 3.4180 | 114.61 KB |
|                                           |                   |           |         |        |           |
| B_Dapper_Cte                              | B_Cte             | 123.36 μs |  0.7324 |      - |  15.63 KB |
| B_Nextorm_Cached_Cte                      | B_Cte             | 142.78 μs |  5.6152 |      - |  92.58 KB |
| B_Linq2Db_Cte                             | B_Cte             | 197.63 μs |  5.6152 |      - |  93.13 KB |
|                                           |                   |           |         |        |           |
| B_Nextorm_Cached_Distinct                 | B_Distinct        |  96.93 μs |  1.8311 |      - |  30.31 KB |
| B_Dapper_Distinct                         | B_Distinct        | 103.02 μs |  0.8545 |      - |  14.45 KB |
| B_Linq2Db_Distinct                        | B_Distinct        | 122.32 μs |  2.3193 |      - |  38.75 KB |
| B_EFCore_Distinct                         | B_Distinct        | 228.04 μs |  7.0801 | 3.4180 | 116.95 KB |
|                                           |                   |           |         |        |           |
| B_Nextorm_Cached_Except                   | B_Except          | 107.16 μs |  2.5635 |      - |  42.97 KB |
| B_Dapper_Except                           | B_Except          | 112.16 μs |  0.8545 |      - |   14.3 KB |
| B_Linq2Db_Except                          | B_Except          | 155.89 μs |  2.9297 |      - |  50.08 KB |
| B_EFCore_Except                           | B_Except          | 265.53 μs |  8.7891 | 4.3945 | 149.22 KB |
|                                           |                   |           |         |        |           |
| B_Dapper_In                               | B_InList          | 103.86 μs |  0.7324 |      - |  12.58 KB |
| B_Nextorm_Cached_In_AtIn_Inline           | B_InList          | 134.29 μs |  4.1504 |      - |  70.39 KB |
| B_Nextorm_Cached_In_ListContains_Inline   | B_InList          | 134.30 μs |  4.1504 |      - |  71.72 KB |
| B_Nextorm_Cached_In_ListContains_Captured | B_InList          | 138.64 μs |  5.3711 |      - |  91.02 KB |
| B_Nextorm_Cached_In_AtIn_Captured         | B_InList          | 139.67 μs |  5.3711 |      - |  89.07 KB |
| B_Linq2Db_In                              | B_InList          | 185.53 μs | 10.4980 |      - | 172.81 KB |
| B_EFCore_In                               | B_InList          | 290.28 μs | 13.1836 | 6.3477 | 221.96 KB |
|                                           |                   |           |         |        |           |
| B_Nextorm_Cached_Intersect                | B_Intersect       | 100.68 μs |  2.5635 |      - |  42.81 KB |
| B_Dapper_Intersect                        | B_Intersect       | 103.64 μs |  0.7324 |      - |  12.81 KB |
| B_Linq2Db_Intersect                       | B_Intersect       | 146.11 μs |  2.9297 |      - |  49.61 KB |
| B_EFCore_Intersect                        | B_Intersect       | 258.24 μs |  8.3008 | 3.9063 | 139.92 KB |
|                                           |                   |           |         |        |           |
| B_Dapper_Join4                            | B_Join4           | 157.26 μs |  1.2207 |      - |  20.16 KB |
| B_Nextorm_Cached_Join4                    | B_Join4           | 164.43 μs |  6.1035 |      - | 100.47 KB |
| B_Linq2Db_Join4                           | B_Join4           | 245.87 μs |  8.5449 |      - | 140.55 KB |
| B_EFCore_Join4                            | B_Join4           | 399.96 μs | 14.6484 | 7.3242 | 239.38 KB |
|                                           |                   |           |         |        |           |
| B_Dapper_LeftJoin                         | B_LeftJoin        | 126.29 μs |  1.2207 |      - |  22.11 KB |
| B_Nextorm_Cached_LeftJoin                 | B_LeftJoin        | 132.73 μs |  3.4180 |      - |  56.41 KB |
| B_Linq2Db_LeftJoin                        | B_LeftJoin        | 222.83 μs |  5.3711 |      - |  89.38 KB |
| B_EFCore_LeftJoin                         | B_LeftJoin        | 314.70 μs | 11.7188 | 5.8594 | 198.84 KB |
|                                           |                   |           |         |        |           |
| B_Dapper_RecursiveCte                     | B_RecursiveCte    | 133.04 μs |  0.7324 |      - |  15.24 KB |
| B_Nextorm_Cached_RecursiveCte             | B_RecursiveCte    | 144.87 μs |  6.1035 |      - | 100.94 KB |
| B_Linq2Db_RecursiveCte                    | B_RecursiveCte    | 199.43 μs |  5.1270 |      - |  84.53 KB |
|                                           |                   |           |         |        |           |
| B_Nextorm_Cached_Contains                 | B_StringFunctions |  85.83 μs |  1.7090 |      - |  28.13 KB |
| B_Dapper_ToUpper                          | B_StringFunctions |  87.23 μs |  0.7324 |      - |  12.74 KB |
| B_Nextorm_Cached_ToUpper                  | B_StringFunctions |  88.02 μs |  1.7090 |      - |  28.67 KB |
| B_Dapper_Contains                         | B_StringFunctions | 108.40 μs |  0.9766 |      - |  17.11 KB |
| B_Linq2Db_Contains                        | B_StringFunctions | 122.27 μs |  2.9297 |      - |  48.91 KB |
| B_Linq2Db_ToUpper                         | B_StringFunctions | 152.84 μs |  3.9063 |      - |  66.33 KB |
| B_EFCore_ToUpper                          | B_StringFunctions | 218.67 μs |  7.5684 | 3.6621 | 127.42 KB |
| B_EFCore_Contains                         | B_StringFunctions | 225.16 μs |  8.0566 | 3.9063 | 134.77 KB |
|                                           |                   |           |         |        |           |
| B_Nextorm_Cached_Udf                      | B_Udf             |  90.31 μs |  1.7090 |      - |  28.99 KB |
| B_Dapper_Udf                              | B_Udf             |  90.89 μs |  0.7324 |      - |  12.74 KB |
| B_Linq2Db_Udf                             | B_Udf             | 159.20 μs |  3.9063 |      - |  66.33 KB |
| B_EFCore_Udf                              | B_Udf             | 233.14 μs |  7.5684 | 3.6621 | 127.42 KB |
|                                           |                   |           |         |        |           |
| B_Nextorm_Cached_SumOver                  | B_Window          | 139.11 μs |  2.4414 |      - |  41.88 KB |
| B_Nextorm_Cached_RowNumber                | B_Window          | 142.04 μs |  2.4414 |      - |  40.86 KB |
| B_Dapper_SumOver                          | B_Window          | 144.25 μs |  0.7324 |      - |  15.39 KB |
| B_Dapper_RowNumber                        | B_Window          | 148.97 μs |  0.7324 |      - |  15.31 KB |
| B_Linq2Db_SumOver                         | B_Window          | 189.84 μs |  3.4180 |      - |  57.89 KB |
| B_Linq2Db_RowNumber                       | B_Window          | 192.61 μs |  3.4180 |      - |  59.53 KB |
